using NzbDrone.Common.Cloud;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataRequestBuilder
    {
        IHttpRequestBuilderFactory GetRequestBuilder();
        IHttpRequestBuilderFactory GetRequestBuilder(string provider);
    }

    public class MetadataRequestBuilder : IMetadataRequestBuilder
    {
        public const string OpenLibraryProvider = "openlibrary";
        public const string RreadingGlassesProvider = "rreading-glasses";

        private readonly IConfigService _configService;
        private readonly IReadarrCloudRequestBuilder _cloudRequestBuilder;

        public MetadataRequestBuilder(IConfigService configService, IReadarrCloudRequestBuilder defaultRequestBuilder)
        {
            _configService = configService;
            _cloudRequestBuilder = defaultRequestBuilder;
        }

        public IHttpRequestBuilderFactory GetRequestBuilder()
        {
            var configuredProvider = NormalizeProviderKey(_configService.MetadataProvider);
            var fallbackProvider = configuredProvider ?? InferProviderFromLegacyOverride();

            return GetRequestBuilder(fallbackProvider ?? RreadingGlassesProvider);
        }

        public IHttpRequestBuilderFactory GetRequestBuilder(string provider)
        {
            var normalizedProvider = NormalizeProviderKey(provider) ?? RreadingGlassesProvider;
            var overrideUrl = GetProviderOverride(normalizedProvider);

            if (overrideUrl.IsNotNullOrWhiteSpace())
            {
                return new HttpRequestBuilder(overrideUrl.TrimEnd('/')).KeepAlive().CreateFactory();
            }

            return normalizedProvider switch
            {
                RreadingGlassesProvider => _cloudRequestBuilder.MetadataRreadingGlasses,
                _ => _cloudRequestBuilder.MetadataOpenLibrary
            };
        }

        private string GetProviderOverride(string provider)
        {
            return provider switch
            {
                RreadingGlassesProvider => FirstNonEmpty(_configService.MetadataRreadingGlassesSource, _configService.MetadataSource),
                _ => FirstNonEmpty(_configService.MetadataOpenLibrarySource, _configService.MetadataSource)
            };
        }

        private string InferProviderFromLegacyOverride()
        {
            if (_configService.MetadataOpenLibrarySource.IsNotNullOrWhiteSpace())
            {
                return OpenLibraryProvider;
            }

            if (_configService.MetadataRreadingGlassesSource.IsNotNullOrWhiteSpace())
            {
                return RreadingGlassesProvider;
            }

            if (_configService.MetadataSource.IsNullOrWhiteSpace())
            {
                return null;
            }

            var normalizedUrl = _configService.MetadataSource.Trim().ToLowerInvariant();
            return normalizedUrl.Contains("bookinfo") || normalizedUrl.Contains("rreading")
                ? RreadingGlassesProvider
                : OpenLibraryProvider;
        }

        private static string NormalizeProviderKey(string provider)
        {
            if (provider.IsNullOrWhiteSpace())
            {
                return null;
            }

            var normalized = provider.Trim().ToLowerInvariant();

            return normalized switch
            {
                "openlibrary" => OpenLibraryProvider,
                "rreading-glasses" => RreadingGlassesProvider,
                "rreadingglasses" => RreadingGlassesProvider,
                "bookinfo" => RreadingGlassesProvider,
                _ => normalized
            };
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var value in values)
            {
                if (value.IsNotNullOrWhiteSpace())
                {
                    return value;
                }
            }

            return null;
        }
    }
}
