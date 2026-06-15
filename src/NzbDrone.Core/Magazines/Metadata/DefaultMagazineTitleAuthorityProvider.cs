using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Magazines.Metadata
{
    public class DefaultMagazineTitleAuthorityProvider : IMagazineTitleAuthorityProvider
    {
        private const string WikidataBaseUrl = "https://www.wikidata.org";
        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IConfigService _configService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public DefaultMagazineTitleAuthorityProvider(IConfigService configService,
                                                     IAppFolderInfo appFolderInfo,
                                                     IDiskProvider diskProvider,
                                                     IHttpClient httpClient,
                                                     Logger logger)
        {
            _configService = configService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _httpClient = httpClient;
            _logger = logger;
        }

        public Task<MagazineAuthorityResult> LookupByTitleAsync(string rawTitle, CancellationToken ct = default)
        {
            if (rawTitle.IsNullOrWhiteSpace())
            {
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            var manualAliasResult = MagazineManualAliasStore.Lookup(_appFolderInfo, _diskProvider, rawTitle);
            if (manualAliasResult != null)
            {
                _logger.Debug("Magazine title authority lookup matched manual alias for {0}", rawTitle);
                return Task.FromResult(manualAliasResult);
            }

            var seedCacheResult = MagazineSeedCache.Lookup(rawTitle);
            if (seedCacheResult != null)
            {
                _logger.Debug("Magazine title authority lookup matched seed cache for {0}", rawTitle);
                return Task.FromResult(seedCacheResult);
            }

            if (_configService.DisableWikidataLookup)
            {
                _logger.Debug("Skipping magazine title authority lookup for {0} because disable flag is enabled", rawTitle);
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            return LookupFromWikidata(rawTitle);
        }

        private Task<MagazineAuthorityResult> LookupFromWikidata(string rawTitle)
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

            try
            {
                var response = _httpClient.Get(request);
                if (response == null || response.HasHttpError || response.Content.IsNullOrWhiteSpace())
                {
                    _logger.Debug("Magazine title authority lookup returned no response for {0}", rawTitle);
                    return Task.FromResult<MagazineAuthorityResult>(null);
                }

                var searchResult = JsonSerializer.Deserialize<WikidataSearchResponse>(response.Content, SerializerSettings);
                var item = searchResult?.Search?.FirstOrDefault();
                if (item == null)
                {
                    _logger.Debug("Magazine title authority lookup returned no matches for {0}", rawTitle);
                    return Task.FromResult<MagazineAuthorityResult>(null);
                }

                var canonicalTitle = item.Label.IsNotNullOrWhiteSpace() ? item.Label : rawTitle.Trim();

                return Task.FromResult(new MagazineAuthorityResult
                {
                    CanonicalTitle = canonicalTitle,
                    NormalizedTitle = MagazineTitleNormalizer.Normalize(canonicalTitle),
                    WikidataId = item.Id,
                    Aliases = item.Aliases?.ToList(),
                    Publisher = item.Description
                });
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Magazine title authority lookup failed for {0}", rawTitle);
                return Task.FromResult<MagazineAuthorityResult>(null);
            }
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
    }
}
