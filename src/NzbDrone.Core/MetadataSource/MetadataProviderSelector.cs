using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;

namespace NzbDrone.Core.MetadataSource
{
    public class MetadataProviderSelector : IProvideAuthorInfo, IProvideBookInfo, ISearchForNewBook, ISearchForNewAuthor, ISearchForNewEntity
    {
        private readonly IConfigService _configService;
        private readonly IReadOnlyList<IMetadataProviderV1> _providers;
        private readonly Logger _logger;

        public MetadataProviderSelector(IConfigService configService, IEnumerable<IMetadataProviderV1> providers, Logger logger)
        {
            _configService = configService;
            _providers = providers
                .OrderByDescending(x => x.Descriptor.Priority)
                .ToList();
            _logger = logger;
        }

        public Author GetAuthorInfo(string readarrId, bool useCache = true)
        {
            return ResolveProviderForIdentifier(readarrId, MetadataProviderCapability.AuthorLookup).GetAuthorInfo(readarrId, useCache);
        }

        public HashSet<string> GetChangedAuthors(DateTime startTime)
        {
            return ResolveProvider(MetadataProviderCapability.ChangeTracking).GetChangedAuthors(startTime);
        }

        public Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id)
        {
            var provider = ResolveProviderForIdentifier(id, MetadataProviderCapability.BookLookup);
            return NormalizeBookInfo(provider.GetBookInfo(id));
        }

        public List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true)
        {
            return SearchBySourcePreference(
                MetadataProviderCapability.BookSearch,
                provider => provider.SearchForNewBook(title, author, getAllEditions));
        }

        public List<Book> SearchByIsbn(string isbn)
        {
            return SearchWithFallback(
                MetadataProviderCapability.IsbnSearch,
                provider => provider.SearchByIsbn(isbn),
                $"ISBN {isbn}");
        }

        public List<Book> SearchByAsin(string asin)
        {
            return SearchWithFallback(
                MetadataProviderCapability.AsinSearch,
                provider => provider.SearchByAsin(asin),
                $"ASIN {asin}");
        }

        public List<Author> SearchForNewAuthor(string title)
        {
            return SearchForAllProviders(
                MetadataProviderCapability.AuthorSearch,
                provider => provider.SearchForNewAuthor(title));
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

        private IMetadataProviderV1 ResolveProviderForIdentifier(string id, MetadataProviderCapability capability)
        {
            if (MetadataIdentifier.TryParse(id, out var identifier))
            {
                var provider = _providers.FirstOrDefault(x => SupportsCapability(x, capability) && x.SupportsIdentifier(identifier));
                if (provider != null)
                {
                    return provider;
                }
            }

            return ResolveProvider(capability);
        }

        private IMetadataProviderV1 ResolveProvider(MetadataProviderCapability capability)
        {
            var provider = GetProviders(capability).FirstOrDefault();
            if (provider != null)
            {
                return provider;
            }

            throw new InvalidOperationException($"No metadata provider is registered for capability {capability}.");
        }

        private IEnumerable<IMetadataProviderV1> GetProviders(MetadataProviderCapability capability)
        {
            var activeProviderKey = NormalizeProviderKey(_configService.MetadataProvider);

            return _providers
                .Where(x => SupportsCapability(x, capability))
                .OrderByDescending(x => string.Equals(x.Descriptor.ProviderKey, activeProviderKey, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(x => x.Descriptor.Priority);
        }

        private IEnumerable<IMetadataProviderV1> GetSearchProviders(MetadataProviderCapability capability)
        {
            var providerKeys = GetConfiguredSearchProviders().ToList();
            if (!providerKeys.Any())
            {
                return GetProviders(capability).Take(1);
            }

            var providers = GetProviders(capability)
                .ToDictionary(x => NormalizeProviderKey(x.Descriptor.ProviderKey), x => x, StringComparer.OrdinalIgnoreCase);

            var orderedProviders = new List<IMetadataProviderV1>();
            var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var providerKey in providerKeys)
            {
                if (providers.TryGetValue(providerKey, out var provider) &&
                    added.Add(providerKey))
                {
                    orderedProviders.Add(provider);
                }
            }

            if (!orderedProviders.Any())
            {
                return GetProviders(capability).Take(1);
            }

            return orderedProviders;
        }

        private IEnumerable<string> GetConfiguredSearchProviders()
        {
            if (_configService.MetadataSearchProviders.IsNullOrWhiteSpace())
            {
                return Array.Empty<string>();
            }

            return _configService.MetadataSearchProviders
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => NormalizeProviderKey(x))
                .Where(x => x.IsNotNullOrWhiteSpace());
        }

        private List<Book> SearchBySourcePreference(MetadataProviderCapability capability,
                                                  Func<IMetadataProviderV1, List<Book>> search)
        {
            var results = new List<Book>();

            foreach (var provider in GetSearchProviders(capability))
            {
                try
                {
                    var searchResults = NormalizeSearchResults(provider, search(provider));
                    results.AddRange(searchResults);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Metadata provider {0} failed while searching for books", provider.Descriptor.ProviderKey);
                }
            }

            return DeduplicateByEditionIdentity(results);
        }

        private List<Author> SearchForAllProviders(MetadataProviderCapability capability,
                                                  Func<IMetadataProviderV1, List<Author>> search)
        {
            var results = new List<Author>();

            foreach (var provider in GetSearchProviders(capability))
            {
                try
                {
                    results.AddRange(search(provider));
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Metadata provider {0} failed while searching for author", provider.Descriptor.ProviderKey);
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduplicated = new List<Author>();

            foreach (var author in results)
            {
                var authorId = author?.Metadata?.Value?.ForeignAuthorId ?? author?.ForeignAuthorId;
                if (authorId.IsNullOrWhiteSpace())
                {
                    deduplicated.Add(author);
                    continue;
                }

                if (seen.Add(authorId))
                {
                    deduplicated.Add(author);
                }
            }

            return deduplicated;
        }

        private List<Book> SearchWithFallback(MetadataProviderCapability capability,
                                              Func<IMetadataProviderV1, List<Book>> search,
                                              string description)
        {
            foreach (var provider in GetProviders(capability))
            {
                try
                {
                    var results = NormalizeSearchResults(provider, search(provider));
                    if (results.Any())
                    {
                        return DeduplicateByEditionIdentity(results);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Metadata provider {0} failed while searching for {1}", provider.Descriptor.ProviderKey, description);
                }
            }

            return new List<Book>();
        }

        private static List<Book> DeduplicateByEditionIdentity(IEnumerable<Book> books)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<Book>();

            foreach (var book in books)
            {
                var editions = book?.Editions?.Value;
                if (editions == null || editions.Count == 0)
                {
                    continue;
                }

                var uniqueEditions = editions
                    .Where(x => seen.Add(MetadataEditionIdentity.GetMatchKey(x)))
                    .ToList();

                if (uniqueEditions.Count == 0)
                {
                    continue;
                }

                var monitored = uniqueEditions.FirstOrDefault(x => x.Monitored) ?? uniqueEditions[0];
                foreach (var edition in uniqueEditions)
                {
                    edition.Monitored = edition == monitored;
                }

                book.Editions = uniqueEditions;
                book.ForeignEditionId = monitored.ForeignEditionId;
                result.Add(book);
            }

            return result;
        }

        private static bool SupportsCapability(IMetadataProviderV1 provider, MetadataProviderCapability capability)
        {
            return (provider.Descriptor.Capabilities & capability) == capability;
        }

        private static string NormalizeProviderKey(string provider)
        {
            return provider?.Trim().ToLowerInvariant();
        }

        private static Tuple<string, Book, List<AuthorMetadata>> NormalizeBookInfo(Tuple<string, Book, List<AuthorMetadata>> tuple)
        {
            if (tuple == null)
            {
                return null;
            }

            HydrateBook(tuple.Item2, tuple.Item1, tuple.Item3);
            return tuple;
        }

        private List<Book> NormalizeSearchResults(IMetadataProviderV1 provider, List<Book> books)
        {
            if (books == null)
            {
                return new List<Book>();
            }

            foreach (var book in books)
            {
                if (book == null)
                {
                    continue;
                }

                if (book.AuthorMetadata?.Value?.ForeignAuthorId.IsNotNullOrWhiteSpace() == true)
                {
                    HydrateBook(book, book.AuthorMetadata.Value.ForeignAuthorId, new List<AuthorMetadata> { book.AuthorMetadata.Value });
                    continue;
                }

                var tuple = NormalizeBookInfo(provider.GetBookInfo(book.ForeignBookId));
                if (tuple?.Item2 != null)
                {
                    book.Author = tuple.Item2.Author;
                    book.AuthorMetadata = tuple.Item2.AuthorMetadata;
                    book.AuthorMetadataId = tuple.Item2.AuthorMetadataId;
                    book.Editions = tuple.Item2.Editions;
                    book.ForeignEditionId = tuple.Item2.ForeignEditionId;
                }
            }

            return books;
        }

        private static void HydrateBook(Book book, string primaryAuthorId, List<AuthorMetadata> authors)
        {
            if (book == null)
            {
                return;
            }

            var primaryAuthor = authors?.FirstOrDefault(x => x.ForeignAuthorId == primaryAuthorId) ?? authors?.FirstOrDefault();
            if (primaryAuthor != null)
            {
                book.AuthorMetadata = primaryAuthor;

                book.Author ??= new Author();
                if (book.Author.Value == null)
                {
                    book.Author = new Author();
                }

                book.Author.Value.Metadata = primaryAuthor;
                book.Author.Value.CleanName = Parser.Parser.CleanAuthorName(primaryAuthor.Name);
                book.Author.Value.AuthorMetadataId = book.AuthorMetadataId;
            }

            if (book.Editions?.Value == null)
            {
                return;
            }

            foreach (var edition in book.Editions.Value)
            {
                edition.Book = book;
            }
        }
    }
}
