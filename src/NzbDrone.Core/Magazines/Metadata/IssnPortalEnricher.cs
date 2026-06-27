using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Magazines.Metadata
{
    /// <summary>
    /// Automated REST access to ISSN Portal is subscriber-only under the portal terms.
    /// This stub is intentionally disabled by default and is for manual verification or
    /// a future licensed subscription, not for bulk harvesting.
    /// </summary>
    public class IssnPortalEnricher : IMagazineTitleAuthorityProvider
    {
        private const string PortalBaseUrl = "https://portal.issn.org";

        private readonly IConfigService _configService;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public IssnPortalEnricher(IConfigService configService, IHttpClient httpClient, Logger logger)
        {
            _configService = configService;
            _httpClient = httpClient;
            _logger = logger;
        }

        public Task<MagazineAuthorityResult> LookupByTitleAsync(string rawTitle, CancellationToken ct = default)
        {
            if (!_configService.EnableIssnPortalLookup)
            {
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            if (rawTitle.IsNullOrWhiteSpace())
            {
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            var normalizedIssn = Issn.Normalize(rawTitle);
            if (normalizedIssn == null)
            {
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            try
            {
                var request = new HttpRequestBuilder(PortalBaseUrl)
                    .Resource($"/resource/ISSN/{normalizedIssn}")
                    .AddQueryParam("format", "json")
                    .Build();

                request.SuppressHttpError = true;
                var response = _httpClient.Get(request);
                if (response == null || response.HasHttpError || response.Content.IsNullOrWhiteSpace())
                {
                    return Task.FromResult<MagazineAuthorityResult>(null);
                }

                return Task.FromResult(ParseJsonLd(response.Content));
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "ISSN Portal lookup failed for {0}", rawTitle);
                return Task.FromResult<MagazineAuthorityResult>(null);
            }
        }

        public static MagazineAuthorityResult ParseJsonLd(string json)
        {
            if (json.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var title = GetString(root, "name")
                            ?? GetString(root, "title")
                            ?? GetString(root, "headline");

                if (title.IsNullOrWhiteSpace())
                {
                    return null;
                }

                var issn = Issn.Normalize(GetString(root, "issn"));
                var issnL = Issn.Normalize(GetString(root, "issnL"));

                return new MagazineAuthorityResult
                {
                    CanonicalTitle = title.Trim(),
                    NormalizedTitle = MagazineTitleNormalizer.Normalize(title),
                    Issn = issn,
                    IssnL = issnL,
                    Country = GetString(root, "country"),
                    Language = GetString(root, "inLanguage"),
                    Publisher = GetNestedString(root, "publisher", "name")
                                 ?? GetString(root, "publisher")
                };
            }
            catch
            {
                return null;
            }
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static string GetNestedString(JsonElement element, string propertyName, string nestedPropertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return GetString(value, nestedPropertyName);
        }
    }
}
