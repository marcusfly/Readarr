using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.ContentTypes;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;

namespace NzbDrone.Core.MetadataSource.RreadingGlasses
{
    public class RreadingGlassesMetadataProvider : IMetadataProviderV1
    {
        private const string ProviderKey = "rreading-glasses";
        private const int MaxResults = 10;

        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            Converters = { new STJUtcConverter() }
        };

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IMetadataRequestBuilder _requestBuilder;
        private readonly Logger _logger;

        public RreadingGlassesMetadataProvider(IHttpClient httpClient,
                                               ICachedHttpResponseService cachedHttpClient,
                                               IMetadataRequestBuilder requestBuilder,
                                               Logger logger)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _requestBuilder = requestBuilder;
            _logger = logger;
        }

        public MetadataProviderDescriptor Descriptor { get; } =
            new MetadataProviderDescriptor(
                ProviderKey,
                "rreading-glasses",
                10,
                MetadataProviderCapability.AuthorLookup |
                MetadataProviderCapability.BookLookup |
                MetadataProviderCapability.AuthorSearch |
                MetadataProviderCapability.BookSearch |
                MetadataProviderCapability.EntitySearch |
                MetadataProviderCapability.IsbnSearch |
                MetadataProviderCapability.AsinSearch |
                MetadataProviderCapability.ChangeTracking,
                LibraryContentType.Book | LibraryContentType.Audiobook);

        public bool SupportsIdentifier(MetadataIdentifier identifier)
        {
            return identifier.Provider == ProviderKey;
        }

        public Author GetAuthorInfo(string readarrId, bool useCache = true)
        {
            var authorId = NormalizeResourceId(readarrId, MetadataEntityType.Author);
            var request = CreateRequest($"author/{authorId}");
            var response = _cachedHttpClient.Get(request, useCache, TimeSpan.FromMinutes(30));

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new AuthorNotFoundException(readarrId);
            }

            if (response.HasHttpError)
            {
                throw new BookInfoException("Unexpected error fetching rreading-glasses author data");
            }

            var resource = Deserialize<RgAuthorResource>(response);
            if (resource == null || resource.ForeignId <= 0)
            {
                throw new BookInfoException($"Invalid rreading-glasses author response for '{readarrId}'");
            }

            return MapAuthor(resource);
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            var workId = NormalizeResourceId(id, MetadataEntityType.Work);
            var request = CreateRequest($"work/{workId}");
            var response = _cachedHttpClient.Get(request, true, TimeSpan.FromMinutes(30));

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new BookNotFoundException(id);
            }

            if (response.HasHttpError)
            {
                throw new BookInfoException("Unexpected error fetching rreading-glasses work data");
            }

            var resource = Deserialize<RgWorkResource>(response);
            if (resource == null || resource.ForeignId <= 0 || resource.Authors?.Count > 0 != true)
            {
                throw new BookInfoException($"Invalid rreading-glasses work response for '{id}'");
            }

            var book = MapBook(resource);
            var authors = (resource.Authors ?? new List<RgAuthorResource>())
                .Where(x => x.ForeignId > 0)
                .GroupBy(x => x.ForeignId)
                .Select(x => MapAuthorMetadata(x.First()))
                .ToList();
            var primaryAuthorId = CreateId(MetadataEntityType.Author, GetAuthorId(resource));

            MapSeriesLinks(resource.Series ?? new List<RgSeriesResource>(), new List<Book> { book }, null);
            return Tuple.Create(primaryAuthorId, book, authors);
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            return Search(title)
                .Select(x => x.Author?.Id ?? 0)
                .Where(x => x > 0)
                .Distinct()
                .Take(MaxResults)
                .Select(x => TryGetAuthor(x.ToString()))
                .Where(x => x != null)
                .ToList();
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var parsed = ParsePrefixedSearch(title);
            if (parsed != null)
            {
                switch (parsed.Value.Prefix)
                {
                    case "author":
                        var foundAuthor = TryGetAuthor(parsed.Value.Value);
                        return foundAuthor?.Books?.Value ?? new List<Book>();
                    case "work":
                        return TryGetWork(parsed.Value.Value);
                    case "edition":
                        return SearchByEditionId(parsed.Value.Value, getAllEditions);
                    case "isbn":
                        return SearchByIsbn(parsed.Value.Value);
                    case "asin":
                        return SearchByAsin(parsed.Value.Value);
                }
            }

            var query = title.Trim();
            if (author.IsNotNullOrWhiteSpace())
            {
                query += " " + author.Trim();
            }

            var results = Search(query);
            if (getAllEditions)
            {
                return results
                    .Select(x => x.WorkId)
                    .Where(x => x > 0)
                    .Distinct()
                    .Take(MaxResults)
                    .SelectMany(x => TryGetWork(x.ToString()))
                    .ToList();
            }

            return results
                .Select(x => x.BookId)
                .Where(x => x > 0)
                .Distinct()
                .Take(MaxResults)
                .SelectMany(x => SearchByEditionId(x.ToString(), false))
                .ToList();
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            var normalized = MetadataIdentifier.NormalizeValue(MetadataEntityType.Isbn, isbn);
            return SearchByLookup($"book/isbn/{normalized}", null);
        }

        public List<Book> SearchByAsin(string asin)
        {
            var normalized = MetadataIdentifier.NormalizeValue(MetadataEntityType.Asin, asin);
            return SearchByLookup($"book/asin/{normalized}", null);
        }

        public List<object> SearchForNewEntity(string title)
        {
            var books = SearchForNewBook(title, null, false);
            var result = new List<object>();

            foreach (var book in books)
            {
                var author = book.Author?.Value;
                if (author != null && !result.Contains(author))
                {
                    result.Add(author);
                }

                result.Add(book);
            }

            return result;
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            var request = CreateRequest("author/changed");
            request.Url = request.Url.AddQueryParam("since", startTime.ToUniversalTime().ToString("o"));
            request.SuppressHttpError = true;

            var response = _httpClient.Get(request);
            if (response.HasHttpError || response.Content.IsNullOrWhiteSpace())
            {
                return null;
            }

            var resource = Deserialize<RgRecentUpdatesResource>(response);
            if (resource == null || resource.Limited)
            {
                return null;
            }

            return resource.Ids
                .Where(x => x > 0)
                .Select(x => CreateId(MetadataEntityType.Author, x))
                .ToHashSet();
        }

        internal static Author MapAuthor(RgAuthorResource resource)
        {
            var metadata = MapAuthorMetadata(resource);
            var books = (resource.Works ?? new List<RgWorkResource>())
                .Where(x => x.ForeignId > 0 && GetAuthorId(x) == resource.ForeignId)
                .GroupBy(x => x.ForeignId)
                .Select(x => MapBook(x.First()))
                .ToList();

            foreach (var book in books)
            {
                book.AuthorMetadata = metadata;
            }

            var seriesResources = resource.Series ?? new List<RgSeriesResource>();
            var series = seriesResources
                .Where(x => x.ForeignId > 0)
                .GroupBy(x => x.ForeignId)
                .Select(x => MapSeries(x.First()))
                .ToList();

            MapSeriesLinks(seriesResources, books, series);

            return new Author
            {
                Metadata = metadata,
                CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                Books = books,
                Series = series
            };
        }

        internal static AuthorMetadata MapAuthorMetadata(RgAuthorResource resource)
        {
            var id = CreateId(MetadataEntityType.Author, resource.ForeignId);
            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = id,
                TitleSlug = id,
                Name = (resource.Name ?? string.Empty).CleanSpaces(),
                Overview = resource.Description,
                Ratings = new Ratings { Votes = resource.RatingCount, Value = (decimal)resource.AverageRating },
                Status = AuthorStatusType.Continuing
            };

            metadata.SortName = metadata.Name.ToLowerInvariant();
            metadata.NameLastFirst = metadata.Name.ToLastFirst();
            metadata.SortNameLastFirst = metadata.NameLastFirst.ToLowerInvariant();

            if (resource.ImageUrl.IsNotNullOrWhiteSpace())
            {
                metadata.Images.Add(new MediaCover.MediaCover
                {
                    Url = resource.ImageUrl,
                    CoverType = MediaCoverTypes.Poster
                });
            }

            AddSourceLink(metadata.Links, resource.Url);
            return metadata;
        }

        internal static Book MapBook(RgWorkResource resource)
        {
            var id = CreateId(MetadataEntityType.Work, resource.ForeignId);
            var book = new Book
            {
                ForeignBookId = id,
                TitleSlug = id,
                Title = FirstNonEmpty(resource.FullTitle, resource.Title, resource.ShortTitle),
                ReleaseDate = resource.ReleaseDate ?? TryParseDate(resource.ReleaseDateRaw),
                Genres = resource.Genres ?? new List<string>(),
                RelatedBooks = resource.RelatedWorks ?? new List<int>(),
                AnyEditionOk = true
            };

            book.CleanTitle = Parser.Parser.CleanAuthorName(book.Title ?? string.Empty);
            AddSourceLink(book.Links, resource.Url);

            book.Editions = (resource.Books ?? new List<RgBookResource>())
                .Where(x => x.ForeignId > 0)
                .GroupBy(x => x.ForeignId)
                .Select(x => MapEdition(x.First()))
                .ToList();

            var best = book.Editions.Value
                .OrderByDescending(x => x.Ratings.Popularity)
                .ThenByDescending(x => x.Isbn13.IsNotNullOrWhiteSpace())
                .ThenBy(x => x.ForeignEditionId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (best != null)
            {
                best.Monitored = true;
                book.ForeignEditionId = best.ForeignEditionId;
                book.Title = FirstNonEmpty(book.Title, best.Title);
            }

            if (!book.ReleaseDate.HasValue)
            {
                book.ReleaseDate = book.Editions.Value
                    .Where(x => x.ReleaseDate.HasValue)
                    .Select(x => x.ReleaseDate)
                    .Min();
            }

            if (resource.RatingCount > 0)
            {
                book.Ratings = new Ratings
                {
                    Votes = resource.RatingCount,
                    Value = (decimal)resource.AverageRating
                };
            }
            else
            {
                var votes = book.Editions.Value.Sum(x => x.Ratings.Votes);
                book.Ratings = new Ratings
                {
                    Votes = votes,
                    Value = votes == 0 ? 0 : book.Editions.Value.Sum(x => x.Ratings.Votes * x.Ratings.Value) / votes
                };
            }

            return book;
        }

        internal static Edition MapEdition(RgBookResource resource)
        {
            var id = CreateId(MetadataEntityType.Edition, resource.ForeignId);
            var edition = new Edition
            {
                ForeignEditionId = id,
                TitleSlug = id,
                Isbn13 = NormalizeIsbn(resource.Isbn13),
                Asin = NormalizeAsin(resource.Asin),
                Title = FirstNonEmpty(resource.FullTitle, resource.Title, resource.ShortTitle).CleanSpaces(),
                Language = resource.Language,
                Overview = resource.Description ?? string.Empty,
                Format = resource.Format,
                IsEbook = resource.IsEbook,
                Disambiguation = resource.EditionInformation,
                Publisher = resource.Publisher,
                PageCount = resource.NumPages ?? 0,
                ReleaseDate = resource.ReleaseDate ?? TryParseDate(resource.ReleaseDateRaw),
                Ratings = new Ratings { Votes = resource.RatingCount, Value = (decimal)resource.AverageRating }
            };

            if (resource.ImageUrl.IsNotNullOrWhiteSpace())
            {
                edition.Images.Add(new MediaCover.MediaCover
                {
                    Url = resource.ImageUrl,
                    CoverType = MediaCoverTypes.Cover
                });
            }

            AddSourceLink(edition.Links, resource.Url);
            return edition;
        }

        private List<RgSearchResource> Search(string query)
        {
            if (query.IsNullOrWhiteSpace())
            {
                return new List<RgSearchResource>();
            }

            var request = CreateRequest("search");
            request.Url = request.Url.AddQueryParam("q", query.Trim());
            request.SuppressHttpError = true;

            var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(1));
            if (response.HasHttpError)
            {
                return new List<RgSearchResource>();
            }

            return Deserialize<List<RgSearchResource>>(response) ?? new List<RgSearchResource>();
        }

        private List<Book> SearchByEditionId(string editionId, bool getAllEditions)
        {
            var normalized = NormalizeResourceId(editionId, MetadataEntityType.Edition);
            return SearchByLookup($"book/{normalized}", getAllEditions ? null : CreateId(MetadataEntityType.Edition, normalized));
        }

        private List<Book> SearchByLookup(string resource, string selectedEditionId)
        {
            var request = CreateRequest(resource);
            request.AllowAutoRedirect = true;
            request.SuppressHttpError = true;

            var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(1));
            if (response.StatusCode == HttpStatusCode.NotFound || response.HasHttpError)
            {
                return new List<Book>();
            }

            var author = Deserialize<RgAuthorResource>(response);
            if (author == null)
            {
                return new List<Book>();
            }

            var mapped = MapAuthor(author);
            var books = mapped.Books?.Value ?? new List<Book>();

            foreach (var book in books)
            {
                book.Author = mapped;
                book.AuthorMetadata = mapped.Metadata.Value;

                if (selectedEditionId.IsNotNullOrWhiteSpace())
                {
                    foreach (var edition in book.Editions.Value)
                    {
                        edition.Monitored = edition.ForeignEditionId == selectedEditionId;
                    }

                    book.ForeignEditionId = selectedEditionId;
                }
            }

            return selectedEditionId.IsNullOrWhiteSpace()
                ? books
                : books.Where(x => x.Editions.Value.Any(e => e.ForeignEditionId == selectedEditionId)).ToList();
        }

        private Author TryGetAuthor(string id)
        {
            try
            {
                return GetAuthorInfo(id);
            }
            catch (BookInfoException ex)
            {
                _logger.Warn(ex, "Unable to hydrate rreading-glasses author {0}", id);
                return null;
            }
        }

        private List<Book> TryGetWork(string id)
        {
            try
            {
                return new List<Book> { GetBookInfo(id).Item2 };
            }
            catch (BookInfoException ex)
            {
                _logger.Warn(ex, "Unable to hydrate rreading-glasses work {0}", id);
                return new List<Book>();
            }
        }

        private HttpRequest CreateRequest(string resource)
        {
            return _requestBuilder.GetRequestBuilder(ProviderKey)
                .Create()
                .Resource(resource)
                .Build();
        }

        private static T Deserialize<T>(HttpResponse response)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(response.Content, SerializerSettings);
            }
            catch (JsonException ex)
            {
                throw new BookInfoException("Invalid response from rreading-glasses", ex);
            }
        }

        private static void MapSeriesLinks(List<RgSeriesResource> resources, List<Book> books, List<Series> mappedSeries)
        {
            var bookById = books.ToDictionary(x => x.ForeignBookId, StringComparer.Ordinal);
            var seriesById = (mappedSeries ??
                              resources
                                  .Where(x => x.ForeignId > 0)
                                  .GroupBy(x => x.ForeignId)
                                  .Select(x => MapSeries(x.First()))
                                  .ToList())
                .ToDictionary(x => x.ForeignSeriesId, StringComparer.Ordinal);

            foreach (var book in books)
            {
                book.SeriesLinks = new List<SeriesBookLink>();
            }

            foreach (var resource in resources)
            {
                if (!seriesById.TryGetValue(CreateId(MetadataEntityType.Series, resource.ForeignId), out var series))
                {
                    continue;
                }

                series.LinkItems = (resource.LinkItems ?? new List<RgSeriesWorkLinkResource>())
                    .Where(x => x.ForeignWorkId > 0)
                    .Select(x => new
                    {
                        Resource = x,
                        BookId = CreateId(MetadataEntityType.Work, x.ForeignWorkId)
                    })
                    .Where(x => bookById.ContainsKey(x.BookId))
                    .Select(x => new SeriesBookLink
                    {
                        Book = bookById[x.BookId],
                        Series = series,
                        IsPrimary = x.Resource.Primary,
                        Position = x.Resource.PositionInSeries,
                        SeriesPosition = x.Resource.SeriesPosition
                    })
                    .ToList();

                foreach (var link in series.LinkItems.Value)
                {
                    link.Book.Value.SeriesLinks.Value.Add(link);
                }
            }
        }

        private static Series MapSeries(RgSeriesResource resource)
        {
            var links = resource.LinkItems ?? new List<RgSeriesWorkLinkResource>();

            return new Series
            {
                ForeignSeriesId = CreateId(MetadataEntityType.Series, resource.ForeignId),
                Title = resource.Title,
                Description = resource.Description,
                WorkCount = links.Count,
                PrimaryWorkCount = links.Count(x => x.Primary)
            };
        }

        private static long GetAuthorId(RgWorkResource resource)
        {
            var authorId = resource.Authors?.FirstOrDefault(x => x.ForeignId > 0)?.ForeignId ?? 0;
            if (authorId > 0)
            {
                return authorId;
            }

            return resource.Books?
                .OrderByDescending(x => x.RatingCount * x.AverageRating)
                .SelectMany(x => x.Contributors ?? new List<RgContributorResource>())
                .FirstOrDefault(x => x.ForeignId > 0)?
                .ForeignId ?? 0;
        }

        private static (string Prefix, string Value)? ParsePrefixedSearch(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var separator = title.IndexOf(':');
            if (separator <= 0 || separator == title.Length - 1)
            {
                return null;
            }

            var prefix = title.Substring(0, separator).Trim().ToLowerInvariant();
            if (!new[] { "author", "work", "edition", "isbn", "asin" }.Contains(prefix))
            {
                return null;
            }

            var value = title.Substring(separator + 1).Trim();
            return value.IsNullOrWhiteSpace() ? null : (prefix, value);
        }

        private static string NormalizeResourceId(string value, MetadataEntityType expectedType)
        {
            if (MetadataIdentifier.TryParse(value, out var identifier))
            {
                if (identifier.Provider != ProviderKey || identifier.EntityType != expectedType)
                {
                    throw new ArgumentException($"Expected a {ProviderKey} {expectedType} identifier.", nameof(value));
                }

                value = identifier.Value;
            }

            if (!long.TryParse(value, out var id) || id <= 0)
            {
                throw new ArgumentException($"'{value}' is not a valid rreading-glasses identifier.", nameof(value));
            }

            return id.ToString();
        }

        private static string CreateId(MetadataEntityType entityType, long value)
        {
            if (value <= 0)
            {
                throw new BookInfoException($"rreading-glasses returned an invalid {entityType} identifier");
            }

            return MetadataIdentifier.Create(ProviderKey, entityType, value.ToString()).ToString();
        }

        private static string CreateId(MetadataEntityType entityType, string value)
        {
            return CreateId(entityType, long.Parse(value));
        }

        private static string NormalizeIsbn(string value)
        {
            try
            {
                return value.IsNullOrWhiteSpace() ? null : MetadataIdentifier.NormalizeValue(MetadataEntityType.Isbn, value);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static string NormalizeAsin(string value)
        {
            try
            {
                return value.IsNullOrWhiteSpace() ? null : MetadataIdentifier.NormalizeValue(MetadataEntityType.Asin, value);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static DateTime? TryParseDate(string value)
        {
            return DateTime.TryParse(value, out var parsed) ? parsed : null;
        }

        private static void AddSourceLink(ICollection<Links> links, string url)
        {
            if (url.IsNotNullOrWhiteSpace())
            {
                links.Add(new Links { Url = url, Name = "rreading-glasses source" });
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(x => x.IsNotNullOrWhiteSpace()) ?? string.Empty;
        }
    }
}
