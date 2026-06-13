using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using LazyCache;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Books;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MediaCover;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    public class BookInfoProxy : IProvideAuthorInfo, IProvideBookInfo, ISearchForNewBook, ISearchForNewAuthor, ISearchForNewEntity
    {
        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            Converters = { new STJUtcConverter(), new OLTextValueConverter() }
        };

        private readonly IHttpClient _httpClient;
        private readonly ICachedHttpResponseService _cachedHttpClient;
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IEditionService _editionService;
        private readonly Logger _logger;
        private readonly IMetadataRequestBuilder _requestBuilder;
        private readonly ICached<HashSet<string>> _cache;
        private readonly CachingService _authorCache;

        public BookInfoProxy(IHttpClient httpClient,
                             ICachedHttpResponseService cachedHttpClient,
                             IAuthorService authorService,
                             IBookService bookService,
                             IEditionService editionService,
                             IMetadataRequestBuilder requestBuilder,
                             Logger logger,
                             ICacheManager cacheManager)
        {
            _httpClient = httpClient;
            _cachedHttpClient = cachedHttpClient;
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _requestBuilder = requestBuilder;
            _cache = cacheManager.GetCache<HashSet<string>>(GetType());
            _logger = logger;

            _authorCache = new CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 })));
            _authorCache.DefaultCachePolicy = new CacheDefaults { DefaultCacheDurationSeconds = 60 };
        }

        // Strips OL key prefix: "/works/OL45883W" → "OL45883W"; "OL23919A" → "OL23919A"
        private static string StripKeyPrefix(string key)
            => key?.TrimStart('/').Split('/').Last();

        private static string AuthorCoverUrl(string olid)
            => $"https://covers.openlibrary.org/a/olid/{olid}-L.jpg";

        private static string WorkCoverUrl(long coverId)
            => $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg";

        private IHttpRequestBuilderFactory OlFactory => _requestBuilder.GetRequestBuilder();

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            // OL recentchanges is high-volume and unfiltered; return null to trigger full periodic refresh.
            return null;
        }

        public HashSet<string> GetChangedBooks(DateTime startTime)
        {
            return _cache.Get("ChangedBooks", () => null, TimeSpan.FromMinutes(30));
        }

        public Author GetAuthorInfo(string foreignAuthorId, bool useCache = true)
        {
            _logger.Debug("Getting Author details for OL author {0}", foreignAuthorId);

            try
            {
                return useCache ? PollAuthor(foreignAuthorId) : PollAuthorUncached(foreignAuthorId);
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Unexpected error getting author info: {foreignAuthorId}", foreignAuthorId);
                throw;
            }
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string foreignBookId)
        {
            try
            {
                return PollBook(foreignBookId);
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Unexpected error getting book info: {foreignBookId}", foreignBookId);
                throw;
            }
        }

        public List<object> SearchForNewEntity(string title)
        {
            var books = SearchForNewBook(title, null, false);

            var result = new List<object>();
            foreach (var book in books)
            {
                var author = book.Author.Value;
                if (!result.Contains(author))
                {
                    result.Add(author);
                }

                result.Add(book);
            }

            return result;
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            try
            {
                var req = OlFactory.Create()
                    .Resource("/search/authors.json")
                    .AddQueryParam("q", title.Trim())
                    .AddQueryParam("limit", "10")
                    .Build();
                req.SuppressHttpError = true;

                var resp = _cachedHttpClient.Get(req, true, TimeSpan.FromHours(1));
                if (resp.HasHttpError)
                {
                    return new List<Author>();
                }

                var searchResult = JsonSerializer.Deserialize<OLAuthorSearchResponse>(resp.Content, SerializerSettings);
                if (searchResult?.Docs == null)
                {
                    return new List<Author>();
                }

                var authors = new List<Author>();
                foreach (var doc in searchResult.Docs.Take(10))
                {
                    var olid = StripKeyPrefix(doc.Key);
                    if (olid.IsNullOrWhiteSpace())
                    {
                        continue;
                    }

                    try
                    {
                        authors.Add(PollAuthor(olid));
                    }
                    catch (Exception e)
                    {
                        _logger.Warn(e, "Failed to fetch author {0}", olid);
                    }
                }

                return authors;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Author search for '{0}' failed", title);
                return new List<Author>();
            }
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            try
            {
                var lowerTitle = title.ToLowerInvariant().Trim();
                var split = lowerTitle.Split(':');
                var prefix = split[0];

                if (split.Length == 2 && new[] { "author", "work", "edition", "isbn", "asin" }.Contains(prefix))
                {
                    var slug = split[1].Trim();
                    if (slug.IsNullOrWhiteSpace() || slug.Any(char.IsWhiteSpace))
                    {
                        return new List<Book>();
                    }

                    switch (prefix)
                    {
                        case "author":
                            return SearchByOlAuthorId(slug);
                        case "work":
                            return SearchByOlWorkId(slug);
                        case "edition":
                            return SearchByOlEditionId(slug, getAllEditions);
                        case "isbn":
                            return SearchByIsbn(slug);
                        case "asin":
                            // No OL ASIN index; fall through to text search
                            return SearchOl(slug, getAllEditions);
                    }
                }

                var q = title.Trim();
                if (author != null)
                {
                    q += " " + author.Trim();
                }

                return SearchOl(q, getAllEditions);
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex, ex.Message);
                throw new BookInfoException("Search for '{0}' failed. Unable to communicate with Open Library.", ex, title);
            }
            catch (Exception ex) when (ex is not BookInfoException)
            {
                _logger.Warn(ex, ex.Message);
                throw new BookInfoException("Search for '{0}' failed. Invalid response received from Open Library.", ex, title);
            }
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            // GET /isbn/{isbn}.json — OL returns the edition directly (no redirect in our HTTP client)
            var req = OlFactory.Create()
                .Resource($"/isbn/{isbn}.json")
                .Build();
            req.SuppressHttpError = true;
            req.AllowAutoRedirect = true;

            HttpResponse resp;
            try
            {
                resp = _httpClient.Get(req);
            }
            catch (Exception e)
            {
                _logger.Warn(e, "ISBN lookup failed for {0}", isbn);
                return new List<Book>();
            }

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return new List<Book>();
            }

            if (resp.HasHttpError)
            {
                return new List<Book>();
            }

            var edition = JsonSerializer.Deserialize<OLEditionResource>(resp.Content, SerializerSettings);
            if (edition?.Works == null || !edition.Works.Any())
            {
                return new List<Book>();
            }

            var workOlid = StripKeyPrefix(edition.Works.First().Key);
            return SearchByOlWorkId(workOlid);
        }

        public List<Book> SearchByAsin(string asin)
        {
            // OL has no ASIN index
            return new List<Book>();
        }

        // Retained for interface compatibility — OL cannot resolve Goodreads integer IDs
        public List<Book> SearchByGoodreadsBookId(int goodreadsId, bool getAllEditions)
        {
            _logger.Debug("SearchByGoodreadsBookId({0}) not supported with Open Library backend", goodreadsId);
            return new List<Book>();
        }

        private List<Book> SearchByOlAuthorId(string authorOlid)
        {
            try
            {
                var author = PollAuthor(authorOlid);
                var books = author.Books.Value;
                var authorMetaDict = new Dictionary<string, AuthorMetadata> { { authorOlid, author.Metadata.Value } };
                foreach (var book in books)
                {
                    AddDbIds(authorOlid, book, authorMetaDict);
                }

                return books;
            }
            catch (AuthorNotFoundException)
            {
                return new List<Book>();
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Error searching by OL author id {0}", authorOlid);
                return new List<Book>();
            }
        }

        private List<Book> SearchByOlWorkId(string workOlid)
        {
            try
            {
                var tuple = GetBookInfo(workOlid);
                AddDbIds(tuple.Item1, tuple.Item2, tuple.Item3.ToDictionary(x => x.ForeignAuthorId));
                return new List<Book> { tuple.Item2 };
            }
            catch (BookNotFoundException)
            {
                return new List<Book>();
            }
            catch (BookInfoException e)
            {
                _logger.Warn(e, "Error searching by OL work id {0}", workOlid);
                return new List<Book>();
            }
        }

        private List<Book> SearchByOlEditionId(string editionOlid, bool getAllEditions)
        {
            var req = OlFactory.Create()
                .Resource($"/books/{editionOlid}.json")
                .Build();
            req.SuppressHttpError = true;
            req.AllowAutoRedirect = true;

            HttpResponse resp;
            try
            {
                resp = _httpClient.Get(req);
            }
            catch (Exception e)
            {
                _logger.Warn(e, "Edition lookup failed for {0}", editionOlid);
                return new List<Book>();
            }

            if (resp.StatusCode == HttpStatusCode.NotFound || resp.HasHttpError)
            {
                return new List<Book>();
            }

            var edition = JsonSerializer.Deserialize<OLEditionResource>(resp.Content, SerializerSettings);
            if (edition?.Works == null || !edition.Works.Any())
            {
                return new List<Book>();
            }

            var workOlid = StripKeyPrefix(edition.Works.First().Key);
            var books = SearchByOlWorkId(workOlid);

            if (!getAllEditions)
            {
                foreach (var book in books)
                {
                    var targetIsbn = edition.Isbn13?.FirstOrDefault() ?? editionOlid;
                    var target = book.Editions.Value.FirstOrDefault(e => e.ForeignEditionId == targetIsbn)
                              ?? book.Editions.Value.FirstOrDefault(e => e.ForeignEditionId == editionOlid);

                    if (target != null)
                    {
                        foreach (var e in book.Editions.Value)
                        {
                            e.Monitored = false;
                        }

                        target.Monitored = true;
                    }
                }
            }

            return books;
        }

        private List<Book> SearchOl(string query, bool getAllEditions)
        {
            var req = OlFactory.Create()
                .Resource("/search.json")
                .AddQueryParam("q", query)
                .AddQueryParam("fields", "key,title,author_name,author_key,first_publish_year,isbn,cover_i,subject,ratings_average,ratings_count")
                .AddQueryParam("limit", "20")
                .Build();
            req.SuppressHttpError = true;

            HttpResponse resp;
            try
            {
                resp = _cachedHttpClient.Get(req, true, TimeSpan.FromHours(1));
            }
            catch (HttpException ex)
            {
                _logger.Warn(ex);
                throw new BookInfoException("Search for '{0}' failed.", ex, query);
            }

            if (resp.HasHttpError || resp.Content.IsNullOrWhiteSpace())
            {
                return new List<Book>();
            }

            var searchResult = JsonSerializer.Deserialize<OLSearchResponse>(resp.Content, SerializerSettings);
            if (searchResult?.Docs == null || !searchResult.Docs.Any())
            {
                return new List<Book>();
            }

            var books = new List<Book>();
            foreach (var doc in searchResult.Docs.Take(10))
            {
                var workOlid = StripKeyPrefix(doc.Key);
                if (workOlid.IsNullOrWhiteSpace())
                {
                    continue;
                }

                try
                {
                    books.AddRange(SearchByOlWorkId(workOlid));
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "Failed to map OL work {0}", workOlid);
                }
            }

            return books;
        }

        private Author PollAuthor(string foreignAuthorId)
        {
            return _authorCache.GetOrAdd(foreignAuthorId,
                () => PollAuthorUncached(foreignAuthorId),
                new LazyCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    ImmediateAbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    Size = 1,
                    SlidingExpiration = TimeSpan.FromMinutes(1),
                    ExpirationMode = ExpirationMode.ImmediateEviction
                }.RegisterPostEvictionCallback((key, value, reason, state) => _logger.Debug($"Clearing cache for {key} due to {reason}")));
        }

        private Author PollAuthorUncached(string foreignAuthorId)
        {
            // GET /authors/{id}.json
            var authorReq = OlFactory.Create()
                .Resource($"/authors/{foreignAuthorId}.json")
                .Build();
            authorReq.SuppressHttpError = true;

            HttpResponse authorResp;
            for (var i = 0; i < 60; i++)
            {
                authorResp = _cachedHttpClient.Get(authorReq, i == 0, TimeSpan.FromMinutes(30));

                if (authorResp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    WaitUntilRetry(authorResp);
                    continue;
                }

                if (authorResp.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new AuthorNotFoundException(foreignAuthorId);
                }

                if (authorResp.HasHttpError)
                {
                    throw new BookInfoException("Unexpected error fetching author data");
                }

                var authorResource = JsonSerializer.Deserialize<OLAuthorResource>(authorResp.Content, SerializerSettings);

                // Fetch works (paginated, cap at 10 pages / 500 works)
                var works = FetchAuthorWorks(foreignAuthorId);

                return MapAuthor(authorResource, works, foreignAuthorId);
            }

            throw new BookInfoException($"Failed to get author data for {foreignAuthorId}");
        }

        private List<OLWorkResource> FetchAuthorWorks(string authorOlid)
        {
            var works = new List<OLWorkResource>();
            var nextPath = $"/authors/{authorOlid}/works.json?limit=50";

            for (var page = 0; page < 10 && nextPath != null; page++)
            {
                var req = OlFactory.Create().Resource(nextPath).Build();
                req.SuppressHttpError = true;

                var resp = _cachedHttpClient.Get(req, true, TimeSpan.FromHours(1));
                if (resp.HasHttpError)
                {
                    break;
                }

                var resource = JsonSerializer.Deserialize<OLAuthorWorksResource>(resp.Content, SerializerSettings);
                if (resource?.Entries != null)
                {
                    works.AddRange(resource.Entries);
                }

                nextPath = resource?.PaginationLinks?.Next;
            }

            return works;
        }

        private Tuple<string, Book, List<AuthorMetadata>> PollBook(string foreignBookId)
        {
            // GET /works/{id}.json
            var workReq = OlFactory.Create()
                .Resource($"/works/{foreignBookId}.json")
                .Build();
            workReq.SuppressHttpError = true;

            HttpResponse workResp;
            for (var i = 0; i < 60; i++)
            {
                workResp = _cachedHttpClient.Get(workReq, i == 0, TimeSpan.FromMinutes(30));

                if (workResp.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    WaitUntilRetry(workResp);
                    continue;
                }

                if (workResp.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BookNotFoundException(foreignBookId);
                }

                if (workResp.HasHttpError)
                {
                    throw new BookInfoException("Unexpected error fetching work data");
                }

                var workResource = JsonSerializer.Deserialize<OLWorkResource>(workResp.Content, SerializerSettings);
                var editions = FetchEditions(foreignBookId);
                var ratings = FetchWorkRatings(foreignBookId);

                var authorMetadata = new List<AuthorMetadata>();
                string primaryAuthorId = null;

                if (workResource.Authors != null)
                {
                    foreach (var authorRef in workResource.Authors.Take(5))
                    {
                        var authorOlid = StripKeyPrefix(authorRef.Author?.Key);
                        if (authorOlid.IsNullOrWhiteSpace())
                        {
                            continue;
                        }

                        try
                        {
                            var author = PollAuthor(authorOlid);
                            authorMetadata.Add(author.Metadata.Value);
                            primaryAuthorId ??= authorOlid;
                        }
                        catch (Exception e)
                        {
                            _logger.Warn(e, "Failed to fetch author {0} for work {1}", authorOlid, foreignBookId);
                        }
                    }
                }

                if (primaryAuthorId == null)
                {
                    throw new BookInfoException($"No valid authors found for work {foreignBookId}");
                }

                var book = MapBook(workResource, editions, ratings);
                return Tuple.Create(primaryAuthorId, book, authorMetadata);
            }

            throw new BookInfoException($"Failed to get work data for {foreignBookId}");
        }

        private List<OLEditionResource> FetchEditions(string workOlid)
        {
            var editions = new List<OLEditionResource>();
            var nextPath = $"/works/{workOlid}/editions.json?limit=50";

            // Cap at 3 pages (150 editions)
            for (var page = 0; page < 3 && nextPath != null; page++)
            {
                var req = OlFactory.Create().Resource(nextPath).Build();
                req.SuppressHttpError = true;

                var resp = _cachedHttpClient.Get(req, true, TimeSpan.FromHours(1));
                if (resp.HasHttpError)
                {
                    break;
                }

                var resource = JsonSerializer.Deserialize<OLEditionsResponse>(resp.Content, SerializerSettings);
                if (resource?.Entries != null)
                {
                    editions.AddRange(resource.Entries);
                }

                nextPath = resource?.Links?.Next;
            }

            return editions;
        }

        private Ratings FetchWorkRatings(string workOlid)
        {
            var req = OlFactory.Create().Resource($"/works/{workOlid}/ratings.json").Build();
            req.SuppressHttpError = true;

            try
            {
                var resp = _cachedHttpClient.Get(req, true, TimeSpan.FromHours(6));
                if (resp.HasHttpError)
                {
                    return new Ratings();
                }

                var resource = JsonSerializer.Deserialize<OLRatingsResponse>(resp.Content, SerializerSettings);
                if (resource?.Summary != null)
                {
                    return new Ratings
                    {
                        Value = (decimal)(resource.Summary.Average ?? 0),
                        Votes = resource.Summary.Count
                    };
                }
            }
            catch (Exception e)
            {
                _logger.Debug(e, "Failed to fetch ratings for work {0}", workOlid);
            }

            return new Ratings();
        }

        private void AddDbIds(string authorId, Book book, Dictionary<string, AuthorMetadata> authors)
        {
            var dbBook = _bookService.FindById(book.ForeignBookId);
            if (dbBook != null)
            {
                book.UseDbFieldsFrom(dbBook);

                var editions = _editionService.GetEditionsByBook(dbBook.Id).ToDictionary(x => x.ForeignEditionId);

                foreach (var edition in book.Editions.Value)
                {
                    edition.Monitored = false;
                    if (editions.TryGetValue(edition.ForeignEditionId, out var dbEdition))
                    {
                        edition.UseDbFieldsFrom(dbEdition);
                    }
                }

                if (book.Editions.Value.Any() && !book.Editions.Value.Any(x => x.Monitored))
                {
                    var mostPopular = book.Editions.Value.OrderByDescending(x => x.Ratings.Popularity).First();
                    mostPopular.Monitored = true;
                }
            }

            var author = _authorService.FindById(authorId);

            if (author == null)
            {
                if (!authors.TryGetValue(authorId, out var metadata))
                {
                    throw new BookInfoException(string.Format("Expected author metadata for id [{0}] in book data {1}", authorId, book));
                }

                author = new Author
                {
                    CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                    Metadata = metadata
                };
            }

            book.Author = author;
            book.AuthorMetadata = author.Metadata.Value;
            book.AuthorMetadataId = author.AuthorMetadataId;
        }

        private void WaitUntilRetry(HttpResponse response)
        {
            var seconds = 5;

            if (response.Headers.ContainsKey("Retry-After"))
            {
                var retryAfter = response.Headers["Retry-After"];
                if (!int.TryParse(retryAfter, out seconds))
                {
                    seconds = 5;
                }
            }

            _logger.Info("Open Library returned 429, backing off for {0}s", seconds);
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
        }

        private static AuthorMetadata MapAuthorMetadata(OLAuthorResource resource, string foreignAuthorId)
        {
            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = foreignAuthorId,
                TitleSlug = foreignAuthorId,
                Name = (resource.PersonalName ?? resource.Name ?? string.Empty).CleanSpaces(),
                Overview = resource.Bio?.Value,
                Aliases = resource.AlternateNames ?? new List<string>(),
                Status = AuthorStatusType.Continuing,
                Ratings = new Ratings { Votes = 0, Value = 0 }
            };

            metadata.SortName = metadata.Name.ToLower();
            metadata.NameLastFirst = metadata.Name.ToLastFirst();
            metadata.SortNameLastFirst = metadata.NameLastFirst.ToLower();

            // Prefer numeric cover id (higher resolution), fall back to OLID-based URL
            if (resource.Photos != null && resource.Photos.Any())
            {
                metadata.Images.Add(new MediaCover.MediaCover
                {
                    Url = $"https://covers.openlibrary.org/a/id/{resource.Photos.First()}-L.jpg",
                    CoverType = MediaCoverTypes.Poster
                });
            }
            else
            {
                metadata.Images.Add(new MediaCover.MediaCover
                {
                    Url = AuthorCoverUrl(foreignAuthorId),
                    CoverType = MediaCoverTypes.Poster
                });
            }

            metadata.Links.Add(new Links
            {
                Url = $"https://openlibrary.org/authors/{foreignAuthorId}",
                Name = "Open Library"
            });

            if (resource.RemoteIds?.Goodreads.IsNotNullOrWhiteSpace() == true)
            {
                metadata.Links.Add(new Links
                {
                    Url = $"https://www.goodreads.com/author/show/{resource.RemoteIds.Goodreads}",
                    Name = "Goodreads"
                });
            }

            if (resource.Wikipedia.IsNotNullOrWhiteSpace())
            {
                metadata.Links.Add(new Links { Url = resource.Wikipedia, Name = "Wikipedia" });
            }

            // Best-effort date parsing — OL uses free-form strings like "18 January 1882", "fl. 1920s"
            metadata.Born = TryParseDate(resource.BirthDate);
            metadata.Died = TryParseDate(resource.DeathDate);

            if (metadata.Died.HasValue)
            {
                metadata.Status = AuthorStatusType.Ended;
            }

            return metadata;
        }

        private static Author MapAuthor(OLAuthorResource resource, List<OLWorkResource> works, string foreignAuthorId)
        {
            var metadata = MapAuthorMetadata(resource, foreignAuthorId);

            var books = works
                .Where(w => w.Key != null)
                .Select(w => MapBook(w, new List<OLEditionResource>(), new Ratings()))
                .ToList();

            books.ForEach(b => b.AuthorMetadata = metadata);

            var series = ExtractSeriesFromWorks(works);
            MapSeriesLinks(series, books, works);

            return new Author
            {
                Metadata = metadata,
                CleanName = Parser.Parser.CleanAuthorName(metadata.Name),
                Books = books,
                Series = series
            };
        }

        private static Book MapBook(OLWorkResource resource, List<OLEditionResource> editionResources, Ratings ratings)
        {
            var workOlid = StripKeyPrefix(resource.Key);

            var book = new Book
            {
                ForeignBookId = workOlid,
                Title = resource.Title ?? string.Empty,
                TitleSlug = workOlid,
                CleanTitle = Parser.Parser.CleanAuthorName(resource.Title ?? string.Empty),
                Genres = resource.Subjects?.Take(10).ToList() ?? new List<string>(),
                RelatedBooks = new List<int>(),
                Ratings = ratings ?? new Ratings(),
                AnyEditionOk = true
            };

            book.Links.Add(new Links
            {
                Url = $"https://openlibrary.org/works/{workOlid}",
                Name = "Open Library"
            });

            book.ReleaseDate = TryParseDate(resource.FirstPublishDate);

            var editions = editionResources.Select(e => MapEdition(e, resource)).ToList();

            if (editions.Any())
            {
                // Monitor the edition with the most metadata completeness
                var best = editions
                    .OrderByDescending(e => (e.PageCount > 0 ? 1 : 0) + (e.Images.Any() ? 1 : 0) + (e.Publisher != null ? 1 : 0) + (e.Isbn13.IsNotNullOrWhiteSpace() ? 2 : 0))
                    .First();
                best.Monitored = true;

                if (book.Title.IsNullOrWhiteSpace())
                {
                    book.Title = best.Title;
                }
            }

            book.Editions = editions;

            // Fall back to earliest edition date if work has no publish date
            if (!book.ReleaseDate.HasValue)
            {
                var editionReleases = book.Editions.Value
                    .Where(x => x.ReleaseDate.HasValue && x.ReleaseDate.Value.Month != 1 && x.ReleaseDate.Value.Day != 1)
                    .ToList();

                if (editionReleases.Any())
                {
                    book.ReleaseDate = editionReleases.Min(x => x.ReleaseDate.Value);
                }
                else
                {
                    editionReleases = book.Editions.Value.Where(x => x.ReleaseDate.HasValue).ToList();
                    if (editionReleases.Any())
                    {
                        book.ReleaseDate = editionReleases.Min(x => x.ReleaseDate.Value);
                    }
                }
            }

            return book;
        }

        private static Edition MapEdition(OLEditionResource resource, OLWorkResource work)
        {
            var editionOlid = StripKeyPrefix(resource.Key);
            var isbn13 = resource.Isbn13?.FirstOrDefault();

            // ISBN-13 is our primary ForeignEditionId; fall back to OL edition OLID for editions without ISBN
            var foreignEditionId = isbn13.IsNotNullOrWhiteSpace() ? isbn13 : editionOlid;

            var lang = resource.Languages?.FirstOrDefault()?.Key?.Split('/').LastOrDefault();
            var publisher = resource.Publishers?.FirstOrDefault();

            var physicalFormat = resource.PhysicalFormat?.ToLowerInvariant() ?? string.Empty;
            var isEbook = physicalFormat.Contains("ebook") || physicalFormat.Contains("epub") ||
                          physicalFormat.Contains("kindle") || physicalFormat.Contains("digital") ||
                          physicalFormat.Contains("electronic");

            var edition = new Edition
            {
                ForeignEditionId = foreignEditionId,
                TitleSlug = foreignEditionId,
                Title = (resource.Title ?? work?.Title ?? string.Empty).CleanSpaces(),
                Language = lang,
                Overview = resource.Description?.Value,
                Publisher = publisher,
                Isbn13 = isbn13,
                PageCount = resource.NumberOfPages ?? 0,
                Format = resource.PhysicalFormat,
                IsEbook = isEbook,
                Disambiguation = resource.Subtitle,
                Ratings = new Ratings { Votes = 0, Value = 0 }
            };

            edition.ReleaseDate = TryParseDate(resource.PublishDate);

            if (resource.Covers != null && resource.Covers.Any())
            {
                edition.Images.Add(new MediaCover.MediaCover
                {
                    Url = WorkCoverUrl(resource.Covers.First()),
                    CoverType = MediaCoverTypes.Cover
                });
            }

            edition.Links.Add(new Links
            {
                Url = $"https://openlibrary.org/books/{editionOlid}",
                Name = "Open Library"
            });

            return edition;
        }

        private static readonly Regex _yearRegex = new Regex(@"\b(\d{4})\b", RegexOptions.Compiled);

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

            // Fallback: extract first 4-digit year
            var m = _yearRegex.Match(raw);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var year) && year >= 100 && year <= DateTime.UtcNow.Year + 5)
            {
                return new DateTime(year, 1, 1);
            }

            return null;
        }

        private static readonly Regex _seriesPosRegex = new Regex(@"^(.+?)\s*[#,]\s*(\d+(?:\.\d+)?)$", RegexOptions.Compiled);

        private static List<Series> ExtractSeriesFromWorks(List<OLWorkResource> works)
        {
            var seriesMap = new Dictionary<string, Series>(StringComparer.OrdinalIgnoreCase);

            foreach (var work in works.Where(w => w.SeriesList != null))
            {
                foreach (var seriesEntry in work.SeriesList)
                {
                    var m = _seriesPosRegex.Match(seriesEntry.Trim());
                    var seriesTitle = m.Success ? m.Groups[1].Value.Trim() : seriesEntry.Trim();
                    var key = seriesTitle.ToLowerInvariant();

                    if (!seriesMap.ContainsKey(key))
                    {
                        seriesMap[key] = new Series
                        {
                            ForeignSeriesId = $"ol-series-{Math.Abs(key.GetHashCode()):x8}",
                            Title = seriesTitle,
                            Description = null
                        };
                    }
                }
            }

            return seriesMap.Values.ToList();
        }

        private static void MapSeriesLinks(List<Series> series, List<Book> books, List<OLWorkResource> works)
        {
            var booksByWorkKey = books
                .Where(b => b.ForeignBookId != null)
                .ToDictionary(b => b.ForeignBookId, StringComparer.OrdinalIgnoreCase);

            var seriesByTitle = series.ToDictionary(s => s.Title, StringComparer.OrdinalIgnoreCase);

            foreach (var book in books)
            {
                book.SeriesLinks = new List<SeriesBookLink>();
            }

            foreach (var work in works.Where(w => w.SeriesList != null && w.Key != null))
            {
                var workOlid = StripKeyPrefix(work.Key);
                if (!booksByWorkKey.TryGetValue(workOlid, out var book))
                {
                    continue;
                }

                foreach (var seriesEntry in work.SeriesList)
                {
                    var m = _seriesPosRegex.Match(seriesEntry.Trim());
                    var seriesTitle = m.Success ? m.Groups[1].Value.Trim() : seriesEntry.Trim();
                    var position = m.Success ? m.Groups[2].Value : null;

                    if (!seriesByTitle.TryGetValue(seriesTitle, out var s))
                    {
                        continue;
                    }

                    var link = new SeriesBookLink
                    {
                        Book = book,
                        Series = s,
                        IsPrimary = true,
                        Position = position,
                        SeriesPosition = 0
                    };

                    book.SeriesLinks.Value.Add(link);
                }
            }
        }
    }
}
