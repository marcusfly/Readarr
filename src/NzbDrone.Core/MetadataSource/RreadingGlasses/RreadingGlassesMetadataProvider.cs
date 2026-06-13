using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
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
        private const int MaxRetryAttempts = 3;

        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            Converters = { new STJUtcConverter(), new OLTextValueConverter() }
        };

        private static readonly Regex SeriesPositionRegex = new Regex(@"^(.+?)\s*[#,]\s*(\d+(?:\.\d+)?)$", RegexOptions.Compiled);

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IMetadataRequestBuilder _requestBuilder;
        private readonly Logger _logger;

        public RreadingGlassesMetadataProvider(IHttpClient httpClient, ICachedHttpResponseService cachedHttpClient, IMetadataRequestBuilder requestBuilder, Logger logger)
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
                MetadataProviderCapability.ChangeTracking);

        public bool SupportsIdentifier(MetadataIdentifier identifier)
        {
            return identifier.Provider == ProviderKey;
        }

        public Author GetAuthorInfo(string readarrId, bool useCache = true)
        {
            var authorId = NormalizeProviderResourceId(readarrId);
            var authorReq = ProviderFactory.Create()
                .Resource($"/authors/{authorId}.json")
                .Build();

            authorReq.SuppressHttpError = true;

            OLAuthorResource authorResource = null;

            for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                var response = _cachedHttpClient.Get(authorReq, useCache && attempt == 0, TimeSpan.FromMinutes(30));

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new AuthorNotFoundException(readarrId);
                }

                if (response.HasHttpError)
                {
                    throw new BookInfoException("Unexpected error fetching rreading-glasses author data");
                }

                authorResource = JsonSerializer.Deserialize<OLAuthorResource>(response.Content, SerializerSettings);
                break;
            }

            if (authorResource == null)
            {
                throw new BookInfoException("Failed to fetch rreading-glasses author data");
            }

            var works = FetchAuthorWorks(authorId);
            return MapAuthor(authorResource, works, authorId);
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            var workId = NormalizeProviderResourceId(id);
            var workReq = ProviderFactory.Create()
                .Resource($"/works/{workId}.json")
                .Build();

            workReq.SuppressHttpError = true;

            OLWorkResource workResource = null;

            for (var attempt = 0; attempt < MaxRetryAttempts; attempt++)
            {
                var response = _cachedHttpClient.Get(workReq, attempt == 0, TimeSpan.FromMinutes(30));

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BookNotFoundException(id);
                }

                if (response.HasHttpError)
                {
                    throw new BookInfoException("Unexpected error fetching rreading-glasses work data");
                }

                workResource = JsonSerializer.Deserialize<OLWorkResource>(response.Content, SerializerSettings);
                break;
            }

            if (workResource == null)
            {
                throw new BookInfoException("Failed to fetch rreading-glasses work data");
            }

            var editions = FetchEditions(workId);
            var ratings = FetchRatings(workId);

            var metadata = new List<AuthorMetadata>();
            string primaryAuthorId = null;

            foreach (var authorRef in workResource.Authors ?? new List<OLWorkAuthorRef>())
            {
                var authorId = NormalizeProviderResourceId(authorRef.Author?.Key);
                if (authorId == null)
                {
                    continue;
                }

                var author = GetAuthorInfo(authorId);
                metadata.Add(author.Metadata.Value);
                primaryAuthorId ??= author.Metadata.Value.ForeignAuthorId;
            }

            if (primaryAuthorId == null)
            {
                throw new BookInfoException("Failed to determine rreading-glasses author");
            }

            var book = MapBook(workResource, editions, ratings);
            return Tuple.Create(primaryAuthorId, book, metadata);
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            var request = ProviderFactory.Create()
                .Resource("/search/authors.json")
                .AddQueryParam("q", title.Trim())
                .AddQueryParam("limit", "10")
                .Build();

            request.SuppressHttpError = true;

            var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(1));
            if (response.HasHttpError)
            {
                return new List<Author>();
            }

            var resource = JsonSerializer.Deserialize<OLAuthorSearchResponse>(response.Content, SerializerSettings);
            if (resource?.Docs == null)
            {
                return new List<Author>();
            }

            return resource.Docs
                .Take(10)
                .Select(d => NormalizeProviderResourceId(d.Key))
                .Where(k => k != null)
                .Select(id => GetAuthorInfo(id))
                .ToList();
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            var lowerTitle = title.ToLowerInvariant().Trim();
            var separatorIndex = lowerTitle.IndexOf(':');

            if (separatorIndex > 0)
            {
                var prefix = lowerTitle[..separatorIndex];
                var slug = title[(separatorIndex + 1) ..].Trim();

                if (prefix == "author")
                {
                    var resolvedAuthorId = NormalizeProviderResourceId(slug);
                    return GetAuthorInfo(resolvedAuthorId).Books.Value;
                }

                if (prefix == "work")
                {
                    return new List<Book> { GetBookInfo(slug).Item2 };
                }

                if (prefix == "edition")
                {
                    return SearchByEditionId(slug, getAllEditions);
                }

                if (prefix == "isbn")
                {
                    return SearchByIsbn(slug);
                }

                if (prefix == "asin")
                {
                    return SearchByAsin(slug);
                }
            }

            var query = title.Trim();
            if (!string.IsNullOrWhiteSpace(author))
            {
                query += " " + author.Trim();
            }

            var request = ProviderFactory.Create()
                .Resource("/search.json")
                .AddQueryParam("q", query)
                .AddQueryParam("fields", "key,title,author_name,author_key,first_publish_year,isbn,asin,cover_i,subject,ratings_average,ratings_count")
                .AddQueryParam("limit", "20")
                .Build();

            request.SuppressHttpError = true;

            var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(1));
            if (response.HasHttpError)
            {
                return new List<Book>();
            }

            var resource = JsonSerializer.Deserialize<OLSearchResponse>(response.Content, SerializerSettings);
            if (resource?.Docs == null)
            {
                return new List<Book>();
            }

            return resource.Docs
                .Take(10)
                .Select(d => NormalizeProviderResourceId(d.Key))
                .Where(k => k != null)
                .Select(id => GetBookInfo(id).Item2)
                .ToList();
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            return SearchByDocumentEndpoint($"/isbn/{MetadataIdentifier.NormalizeValue(MetadataEntityType.Isbn, isbn)}.json");
        }

        public List<Book> SearchByAsin(string asin)
        {
            return SearchByDocumentEndpoint($"/asin/{MetadataIdentifier.NormalizeValue(MetadataEntityType.Asin, asin)}.json");
        }

        public List<object> SearchForNewEntity(string title)
        {
            var books = SearchForNewBook(title, null, false);
            var result = new List<object>();

            foreach (var book in books)
            {
                if (!result.Contains(book.Author.Value))
                {
                    result.Add(book.Author.Value);
                }

                result.Add(book);
            }

            return result;
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            var request = ProviderFactory.Create()
                .Resource("/authors/changed.json")
                .AddQueryParam("since", startTime.ToString("o"))
                .Build();

            request.SuppressHttpError = true;

            var response = _httpClient.Get(request);
            if (response.HasHttpError || string.IsNullOrWhiteSpace(response.Content))
            {
                return null;
            }

            using var document = JsonDocument.Parse(response.Content);
            var ids = new HashSet<string>(StringComparer.Ordinal);

            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                AddChangedAuthorIds(document.RootElement, ids);
            }
            else if (document.RootElement.ValueKind == JsonValueKind.Object &&
                     document.RootElement.TryGetProperty("authors", out var authors))
            {
                AddChangedAuthorIds(authors, ids);
            }

            return ids.Count == 0 ? null : ids;
        }

        private IHttpRequestBuilderFactory ProviderFactory => _requestBuilder.GetRequestBuilder(ProviderKey);

        private List<Book> SearchByEditionId(string editionId, bool getAllEditions)
        {
            var request = ProviderFactory.Create()
                .Resource($"/books/{NormalizeProviderResourceId(editionId)}.json")
                .Build();

            request.SuppressHttpError = true;
            var response = _httpClient.Get(request);

            if (response.HasHttpError)
            {
                return new List<Book>();
            }

            var edition = JsonSerializer.Deserialize<OLEditionResource>(response.Content, SerializerSettings);
            var workKey = edition?.Works?.FirstOrDefault()?.Key;
            if (workKey == null)
            {
                return new List<Book>();
            }

            var books = new List<Book> { GetBookInfo(workKey).Item2 };

            if (!getAllEditions)
            {
                var namespacedEditionId = CreateEditionId(NormalizeProviderResourceId(editionId));
                foreach (var candidate in books)
                {
                    foreach (var existing in candidate.Editions.Value)
                    {
                        existing.Monitored = existing.ForeignEditionId == namespacedEditionId;
                    }
                }
            }

            return books;
        }

        private List<Book> SearchByDocumentEndpoint(string resourcePath)
        {
            var request = ProviderFactory.Create()
                .Resource(resourcePath)
                .Build();

            request.SuppressHttpError = true;
            var response = _httpClient.Get(request);

            if (response.HasHttpError)
            {
                return new List<Book>();
            }

            var edition = JsonSerializer.Deserialize<OLEditionResource>(response.Content, SerializerSettings);
            var workKey = edition?.Works?.FirstOrDefault()?.Key;
            if (workKey == null)
            {
                return new List<Book>();
            }

            return new List<Book> { GetBookInfo(workKey).Item2 };
        }

        private List<OLWorkResource> FetchAuthorWorks(string authorId)
        {
            var works = new List<OLWorkResource>();
            var nextPath = $"/authors/{authorId}/works.json?limit=50";

            for (var page = 0; page < 10 && nextPath != null; page++)
            {
                var request = ProviderFactory.Create().Resource(nextPath).Build();
                request.SuppressHttpError = true;

                var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(1));
                if (response.HasHttpError)
                {
                    break;
                }

                var resource = JsonSerializer.Deserialize<OLAuthorWorksResource>(response.Content, SerializerSettings);
                works.AddRange(resource?.Entries ?? new List<OLWorkResource>());
                nextPath = resource?.PaginationLinks?.Next;
            }

            return works;
        }

        private List<OLEditionResource> FetchEditions(string workId)
        {
            var editions = new List<OLEditionResource>();
            var nextPath = $"/works/{workId}/editions.json?limit=50";

            for (var page = 0; page < 3 && nextPath != null; page++)
            {
                var request = ProviderFactory.Create().Resource(nextPath).Build();
                request.SuppressHttpError = true;

                var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(1));
                if (response.HasHttpError)
                {
                    break;
                }

                var resource = JsonSerializer.Deserialize<OLEditionsResponse>(response.Content, SerializerSettings);
                editions.AddRange(resource?.Entries ?? new List<OLEditionResource>());
                nextPath = resource?.Links?.Next;
            }

            return editions;
        }

        private Ratings FetchRatings(string workId)
        {
            var request = ProviderFactory.Create()
                .Resource($"/works/{workId}/ratings.json")
                .Build();

            request.SuppressHttpError = true;
            var response = _cachedHttpClient.Get(request, true, TimeSpan.FromHours(6));
            if (response.HasHttpError)
            {
                return new Ratings();
            }

            var resource = JsonSerializer.Deserialize<OLRatingsResponse>(response.Content, SerializerSettings);

            return new Ratings
            {
                Value = (decimal)(resource?.Summary?.Average ?? 0),
                Votes = resource?.Summary?.Count ?? 0
            };
        }

        private static Author MapAuthor(OLAuthorResource resource, List<OLWorkResource> works, string authorId)
        {
            var metadata = MapAuthorMetadata(resource, authorId);
            var books = works
                .Where(w => w.Key != null)
                .Select(w => MapBook(w, new List<OLEditionResource>(), new Ratings()))
                .ToList();

            books.ForEach(b => b.AuthorMetadata = metadata);
            var series = ExtractSeriesFromWorks(works, authorId);
            MapSeriesLinks(series, books, works);

            return new Author
            {
                Metadata = metadata,
                CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                Books = books,
                Series = series
            };
        }

        private static AuthorMetadata MapAuthorMetadata(OLAuthorResource resource, string authorId)
        {
            var metadataId = CreateAuthorId(authorId);
            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = metadataId,
                TitleSlug = metadataId,
                Name = (resource.PersonalName ?? resource.Name ?? string.Empty).CleanSpaces(),
                Overview = resource.Bio?.Value,
                Aliases = resource.AlternateNames ?? new List<string>(),
                Status = AuthorStatusType.Continuing,
                Ratings = new Ratings()
            };

            metadata.SortName = metadata.Name.ToLowerInvariant();
            metadata.NameLastFirst = metadata.Name.ToLastFirst();
            metadata.SortNameLastFirst = metadata.NameLastFirst.ToLowerInvariant();

            if (resource.Photos?.Any() == true)
            {
                metadata.Images.Add(new MediaCover.MediaCover
                {
                    Url = $"https://covers.openlibrary.org/a/id/{resource.Photos.First()}-L.jpg",
                    CoverType = MediaCoverTypes.Poster
                });
            }

            metadata.Links.Add(new Links
            {
                Url = $"{ProviderKey}:authors/{NormalizeProviderResourceId(authorId)}",
                Name = "rreading-glasses"
            });

            metadata.Born = TryParseDate(resource.BirthDate);
            metadata.Died = TryParseDate(resource.DeathDate);

            if (metadata.Died.HasValue)
            {
                metadata.Status = AuthorStatusType.Ended;
            }

            return metadata;
        }

        private static Book MapBook(OLWorkResource resource, List<OLEditionResource> editions, Ratings ratings)
        {
            var workId = NormalizeProviderResourceId(resource.Key);
            var foreignBookId = CreateWorkId(workId);

            var book = new Book
            {
                ForeignBookId = foreignBookId,
                TitleSlug = foreignBookId,
                Title = resource.Title ?? string.Empty,
                CleanTitle = Parser.Parser.CleanAuthorName(resource.Title ?? string.Empty),
                Genres = resource.Subjects?.Take(10).ToList() ?? new List<string>(),
                RelatedBooks = new List<int>(),
                Ratings = ratings ?? new Ratings(),
                AnyEditionOk = true
            };

            book.ReleaseDate = TryParseDate(resource.FirstPublishDate);
            book.Links.Add(new Links
            {
                Url = $"{ProviderKey}:works/{workId}",
                Name = "rreading-glasses"
            });

            book.Editions = editions.Select(e => MapEdition(e, resource)).ToList();
            if (book.Editions.Value.Any())
            {
                var best = book.Editions.Value.First();
                best.Monitored = true;
                book.ForeignEditionId = best.ForeignEditionId;
            }

            return book;
        }

        private static Edition MapEdition(OLEditionResource resource, OLWorkResource work)
        {
            var editionId = NormalizeProviderResourceId(resource.Key);
            var foreignEditionId = CreateEditionId(editionId);

            return new Edition
            {
                ForeignEditionId = foreignEditionId,
                TitleSlug = foreignEditionId,
                Title = (resource.Title ?? work?.Title ?? string.Empty).CleanSpaces(),
                Language = resource.Languages?.FirstOrDefault()?.Key?.Split('/').LastOrDefault(),
                Overview = resource.Description?.Value,
                Publisher = resource.Publishers?.FirstOrDefault(),
                Isbn13 = resource.Isbn13?.FirstOrDefault(),
                PageCount = resource.NumberOfPages ?? 0,
                Format = resource.PhysicalFormat,
                IsEbook = (resource.PhysicalFormat ?? string.Empty).ToLowerInvariant().Contains("ebook"),
                Disambiguation = resource.Subtitle,
                ReleaseDate = TryParseDate(resource.PublishDate),
                Ratings = new Ratings()
            };
        }

        private static List<Series> ExtractSeriesFromWorks(List<OLWorkResource> works, string authorScope)
        {
            var series = new Dictionary<string, Series>(StringComparer.OrdinalIgnoreCase);

            foreach (var work in works.Where(w => w.SeriesList != null))
            {
                foreach (var entry in work.SeriesList)
                {
                    var match = SeriesPositionRegex.Match(entry.Trim());
                    var title = match.Success ? match.Groups[1].Value.Trim() : entry.Trim();
                    var normalized = NormalizeSeriesTitle(title);

                    if (!series.ContainsKey(normalized))
                    {
                        series[normalized] = new Series
                        {
                            ForeignSeriesId = CreateSeriesId(authorScope, title),
                            Title = title
                        };
                    }
                }
            }

            return series.Values.ToList();
        }

        private static void MapSeriesLinks(List<Series> series, List<Book> books, List<OLWorkResource> works)
        {
            var booksByWork = books.ToDictionary(b => NormalizeProviderResourceId(b.ForeignBookId), StringComparer.OrdinalIgnoreCase);
            var seriesByTitle = series.ToDictionary(s => s.Title, StringComparer.OrdinalIgnoreCase);

            foreach (var book in books)
            {
                book.SeriesLinks = new List<SeriesBookLink>();
            }

            foreach (var work in works.Where(w => w.SeriesList != null))
            {
                var workId = NormalizeProviderResourceId(work.Key);
                if (!booksByWork.TryGetValue(workId, out var book))
                {
                    continue;
                }

                foreach (var entry in work.SeriesList)
                {
                    var match = SeriesPositionRegex.Match(entry.Trim());
                    var title = match.Success ? match.Groups[1].Value.Trim() : entry.Trim();
                    var position = match.Success ? match.Groups[2].Value : null;

                    if (!seriesByTitle.TryGetValue(title, out var seriesEntry))
                    {
                        continue;
                    }

                    book.SeriesLinks.Value.Add(new SeriesBookLink
                    {
                        Book = book,
                        Series = seriesEntry,
                        IsPrimary = true,
                        Position = position,
                        SeriesPosition = 0
                    });
                }
            }
        }

        private static string CreateAuthorId(string rawId)
        {
            return MetadataIdentifier.Create(ProviderKey, MetadataEntityType.Author, NormalizeProviderResourceId(rawId)).ToString();
        }

        private static string CreateWorkId(string rawId)
        {
            return MetadataIdentifier.Create(ProviderKey, MetadataEntityType.Work, NormalizeProviderResourceId(rawId)).ToString();
        }

        private static string CreateEditionId(string rawId)
        {
            return MetadataIdentifier.Create(ProviderKey, MetadataEntityType.Edition, NormalizeProviderResourceId(rawId)).ToString();
        }

        private static string CreateSeriesId(string authorScope, string seriesTitle)
        {
            return DerivedMetadataIdGenerator.Create(ProviderKey, MetadataEntityType.Series, NormalizeProviderResourceId(authorScope), NormalizeSeriesTitle(seriesTitle)).ToString();
        }

        private static string NormalizeProviderResourceId(string value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (MetadataIdentifier.TryParse(value, out var identifier))
            {
                return identifier.Value;
            }

            return value.Trim().TrimStart('/').Split('/').Last();
        }

        private static string NormalizeSeriesTitle(string value)
        {
            return Regex.Replace(value?.Trim() ?? string.Empty, @"\s+", " ").ToLowerInvariant();
        }

        private static DateTime? TryParseDate(string raw)
        {
            if (raw.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (DateTime.TryParse(raw, out var parsed))
            {
                return parsed;
            }

            var match = Regex.Match(raw, @"\b(\d{4})\b");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var year))
            {
                return new DateTime(year, 1, 1);
            }

            return null;
        }

        private static void AddChangedAuthorIds(JsonElement container, ISet<string> ids)
        {
            if (container.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in container.EnumerateArray())
            {
                string raw = null;

                if (item.ValueKind == JsonValueKind.Number)
                {
                    raw = item.GetInt64().ToString();
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    raw = item.GetString();
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    raw = TryGetProperty(item, "id") ??
                          TryGetProperty(item, "author_id") ??
                          TryGetProperty(item, "key");
                }

                if (!raw.IsNullOrWhiteSpace())
                {
                    ids.Add(CreateAuthorId(raw));
                }
            }
        }

        private static string TryGetProperty(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var property))
            {
                return null;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number => property.GetInt64().ToString(),
                JsonValueKind.String => property.GetString(),
                _ => null
            };
        }
    }
}
