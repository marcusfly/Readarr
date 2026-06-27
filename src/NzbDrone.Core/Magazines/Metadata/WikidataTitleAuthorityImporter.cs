using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Magazines.Metadata
{
    public class WikidataTitleAuthorityImporter
    {
        private const string WikidataBaseUrl = "https://www.wikidata.org";
        private const string WikidataQueryBaseUrl = "https://query.wikidata.org";
        private const string CacheFileName = "magazine_wikidata_cache.json";

        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IConfigService _configService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IHttpClient _httpClient;
        private readonly IRateLimitService _rateLimitService;
        private readonly Logger _logger;
        private readonly Lazy<ConcurrentDictionary<string, MagazineAuthorityResult>> _cache;
        private readonly object _cacheLock = new object();
        private readonly string _cachePath;

        public WikidataTitleAuthorityImporter(IConfigService configService,
                                              IAppFolderInfo appFolderInfo,
                                              IDiskProvider diskProvider,
                                              IHttpClient httpClient,
                                              IRateLimitService rateLimitService,
                                              Logger logger)
        {
            _configService = configService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _httpClient = httpClient;
            _rateLimitService = rateLimitService;
            _logger = logger;
            _cachePath = GetCachePath();
            _cache = new Lazy<ConcurrentDictionary<string, MagazineAuthorityResult>>(LoadCache, true);
        }

        public async Task<MagazineAuthorityResult> LookupByTitleAsync(string rawTitle, CancellationToken ct = default)
        {
            if (rawTitle.IsNullOrWhiteSpace() || _configService.DisableWikidataLookup)
            {
                return null;
            }

            var normalized = MagazineTitleNormalizer.Normalize(rawTitle);
            if (normalized.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (_cache.Value.TryGetValue(normalized, out var cached))
            {
                if (!NeedsRefresh(cached))
                {
                    return cached;
                }
            }

            try
            {
                await (_rateLimitService.WaitAndPulseAsync(GetType().FullName, TimeSpan.FromSeconds(1)) ?? Task.CompletedTask);

                var searchResponse = await GetSearchResponseAsync(rawTitle, ct);
                var item = searchResponse?.Search?.FirstOrDefault();
                if (item == null)
                {
                    return cached;
                }

                var result = CreateSearchResult(item, rawTitle);

                var details = await GetDetailsAsync(item.Id, ct);
                if (details != null)
                {
                    result.Issn = details.Issn;
                    result.IssnL = details.IssnL;
                    result.Publisher = details.Publisher ?? result.Publisher;
                    result.Country = details.Country;
                    result.Language = details.Language;
                    result.ImageUrl = NormalizeMediaUrl(details.ImageUrl);
                    result.LogoUrl = NormalizeMediaUrl(details.LogoUrl);
                    result.OfficialWebsite = details.OfficialWebsite;
                }

                if (result.Issn.IsNotNullOrWhiteSpace())
                {
                    result.Issn = Issn.Normalize(result.Issn);
                }

                if (result.IssnL.IsNotNullOrWhiteSpace())
                {
                    result.IssnL = Issn.Normalize(result.IssnL);
                }

                _cache.Value[normalized] = result;
                SaveCache();

                return result;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Magazine title authority lookup failed for {0}", rawTitle);
                return cached;
            }
        }

        private static bool NeedsRefresh(MagazineAuthorityResult cached)
        {
            if (cached == null)
            {
                return true;
            }

            return cached.ImageUrl.IsNullOrWhiteSpace() &&
                   cached.LogoUrl.IsNullOrWhiteSpace() &&
                   cached.OfficialWebsite.IsNullOrWhiteSpace();
        }

        private static MagazineAuthorityResult CreateSearchResult(WikidataSearchItem item, string rawTitle)
        {
            var canonicalTitle = item.Label.IsNotNullOrWhiteSpace() ? item.Label : rawTitle.Trim();

            return new MagazineAuthorityResult
            {
                CanonicalTitle = canonicalTitle,
                NormalizedTitle = MagazineTitleNormalizer.Normalize(canonicalTitle),
                WikidataId = item.Id,
                Aliases = item.Aliases?.ToList(),
                Publisher = item.Description,
                Description = item.Description
            };
        }

        private Task<WikidataSearchResponse> GetSearchResponseAsync(string rawTitle, CancellationToken ct)
        {
            var request = new HttpRequestBuilder(WikidataBaseUrl)
                .Resource("/w/api.php")
                .AddQueryParam("action", "wbsearchentities")
                .AddQueryParam("search", rawTitle.Trim())
                .AddQueryParam("language", "en")
                .AddQueryParam("format", "json")
                .AddQueryParam("limit", "5")
                .Build();

            request.SuppressHttpError = true;
            var response = _httpClient.Get(request);
            if (response == null || response.HasHttpError || response.Content.IsNullOrWhiteSpace())
            {
                return Task.FromResult<WikidataSearchResponse>(null);
            }

            return Task.FromResult(JsonSerializer.Deserialize<WikidataSearchResponse>(response.Content, SerializerSettings));
        }

        private Task<WikidataDetails> GetDetailsAsync(string wikidataId, CancellationToken ct)
        {
            if (wikidataId.IsNullOrWhiteSpace())
            {
                return Task.FromResult<WikidataDetails>(null);
            }

            (_rateLimitService.WaitAndPulseAsync(GetType().FullName, "sparql", TimeSpan.FromSeconds(1)) ?? Task.CompletedTask).GetAwaiter().GetResult();

            var query = $@"
SELECT ?issn ?issnL ?publisherLabel ?countryLabel ?languageLabel ?image ?logo ?officialWebsite WHERE {{
  VALUES ?item {{ wd:{wikidataId} }}
  OPTIONAL {{ ?item wdt:P236 ?issn. }}
  OPTIONAL {{ ?item wdt:P7363 ?issnL. }}
  OPTIONAL {{ ?item wdt:P123 ?publisher. }}
  OPTIONAL {{ ?item wdt:P17 ?country. }}
  OPTIONAL {{ ?item wdt:P407 ?language. }}
  OPTIONAL {{ ?item wdt:P18 ?image. }}
  OPTIONAL {{ ?item wdt:P154 ?logo. }}
  OPTIONAL {{ ?item wdt:P856 ?officialWebsite. }}
  SERVICE wikibase:label {{ bd:serviceParam wikibase:language ""en"". }}
}} LIMIT 1";

            var request = new HttpRequestBuilder(WikidataQueryBaseUrl)
                .Resource("/sparql")
                .AddQueryParam("format", "json")
                .AddQueryParam("query", query)
                .Build();

            request.SuppressHttpError = true;
            var response = _httpClient.Get(request);
            if (response == null || response.HasHttpError || response.Content.IsNullOrWhiteSpace())
            {
                return Task.FromResult<WikidataDetails>(null);
            }

            var payload = JsonSerializer.Deserialize<WikidataSparqlResponse>(response.Content, SerializerSettings);
            var binding = payload?.Results?.Bindings?.FirstOrDefault();
            if (binding == null)
            {
                return Task.FromResult<WikidataDetails>(null);
            }

            return Task.FromResult(new WikidataDetails
            {
                Issn = binding.Issn?.Value,
                IssnL = binding.IssnL?.Value,
                Publisher = binding.PublisherLabel?.Value,
                Country = binding.CountryLabel?.Value,
                Language = binding.LanguageLabel?.Value,
                ImageUrl = binding.Image?.Value,
                LogoUrl = binding.Logo?.Value,
                OfficialWebsite = binding.OfficialWebsite?.Value
            });
        }

        private static string NormalizeMediaUrl(string url)
        {
            if (url.IsNullOrWhiteSpace())
            {
                return null;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + url.Substring("http://".Length);
            }

            return url;
        }

        private ConcurrentDictionary<string, MagazineAuthorityResult> LoadCache()
        {
            var cache = new ConcurrentDictionary<string, MagazineAuthorityResult>(StringComparer.OrdinalIgnoreCase);
            if (_cachePath.IsNullOrWhiteSpace() || !_diskProvider.FileExists(_cachePath))
            {
                return cache;
            }

            try
            {
                var json = _diskProvider.ReadAllText(_cachePath);
                if (json.IsNullOrWhiteSpace())
                {
                    return cache;
                }

                var entries = JsonSerializer.Deserialize<Dictionary<string, MagazineAuthorityResult>>(json, SerializerSettings);
                if (entries == null)
                {
                    return cache;
                }

                foreach (var entry in entries)
                {
                    if (entry.Key.IsNullOrWhiteSpace() || entry.Value == null)
                    {
                        continue;
                    }

                    cache[entry.Key] = entry.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Failed to read magazine Wikidata cache from {0}", _cachePath);
            }

            return cache;
        }

        private void SaveCache()
        {
            if (_cachePath.IsNullOrWhiteSpace())
            {
                return;
            }

            lock (_cacheLock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_cachePath);
                    if (directory.IsNotNullOrWhiteSpace())
                    {
                        _diskProvider.EnsureFolder(directory);
                    }

                    var snapshot = _cache.Value.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
                    var json = JsonSerializer.Serialize(snapshot, SerializerSettings);
                    _diskProvider.WriteAllText(_cachePath, json);
                }
                catch (Exception ex)
                {
                    _logger.Debug(ex, "Failed to write magazine Wikidata cache to {0}", _cachePath);
                }
            }
        }

        private string GetCachePath()
        {
            var appDataPath = _appFolderInfo?.GetAppDataPath();
            if (appDataPath.IsNullOrWhiteSpace())
            {
                return null;
            }

            return Path.Combine(appDataPath, CacheFileName);
        }

        private class WikidataSearchResponse
        {
            public List<WikidataSearchItem> Search { get; set; }
        }

        private class WikidataSearchItem
        {
            public string Id { get; set; }
            public string Label { get; set; }
            public string Description { get; set; }
            public List<string> Aliases { get; set; }
        }

        private class WikidataSparqlResponse
        {
            public WikidataSparqlResults Results { get; set; }
        }

        private class WikidataSparqlResults
        {
            public List<WikidataSparqlBinding> Bindings { get; set; }
        }

        private class WikidataSparqlBinding
        {
            public WikidataSparqlValue Issn { get; set; }
            public WikidataSparqlValue IssnL { get; set; }
            public WikidataSparqlValue PublisherLabel { get; set; }
            public WikidataSparqlValue CountryLabel { get; set; }
            public WikidataSparqlValue LanguageLabel { get; set; }
            public WikidataSparqlValue Image { get; set; }
            public WikidataSparqlValue Logo { get; set; }
            public WikidataSparqlValue OfficialWebsite { get; set; }
        }

        private class WikidataSparqlValue
        {
            public string Type { get; set; }
            public string Value { get; set; }
        }

        private class WikidataDetails
        {
            public string Issn { get; set; }
            public string IssnL { get; set; }
            public string Publisher { get; set; }
            public string Country { get; set; }
            public string Language { get; set; }
            public string ImageUrl { get; set; }
            public string LogoUrl { get; set; }
            public string OfficialWebsite { get; set; }
        }
    }
}
