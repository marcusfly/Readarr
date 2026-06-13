using NzbDrone.Core.Configuration;
using Readarr.Http.REST;

namespace Prowlarr.Api.V1.Config
{
    public class DevelopmentConfigResource : RestResource
    {
        public string MetadataProvider { get; set; }
        public string MetadataSource { get; set; }
        public string MetadataOpenLibrarySource { get; set; }
        public string MetadataRreadingGlassesSource { get; set; }
        public string ConsoleLogLevel { get; set; }
        public bool LogSql { get; set; }
        public int LogRotate { get; set; }
        public bool FilterSentryEvents { get; set; }
    }

    public static class DevelopmentConfigResourceMapper
    {
        public static DevelopmentConfigResource ToResource(this IConfigFileProvider model, IConfigService configService)
        {
            return new DevelopmentConfigResource
            {
                MetadataProvider = configService.MetadataProvider,
                MetadataSource = configService.MetadataSource,
                MetadataOpenLibrarySource = configService.MetadataOpenLibrarySource,
                MetadataRreadingGlassesSource = configService.MetadataRreadingGlassesSource,
                ConsoleLogLevel = model.ConsoleLogLevel,
                LogSql = model.LogSql,
                LogRotate = model.LogRotate,
                FilterSentryEvents = model.FilterSentryEvents
            };
        }
    }
}
