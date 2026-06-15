using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Magazines
{
    public static class MagazineFormatDetector
    {
        public static QualityModel DetectQuality(string filename)
        {
            if (filename.IsNullOrWhiteSpace())
            {
                return new QualityModel { Quality = Quality.Unknown };
            }

            var extensionQuality = GetQualityFromExtension(filename.GetPathExtension());
            if (extensionQuality != Quality.Unknown)
            {
                return new QualityModel { Quality = extensionQuality };
            }

            return QualityParser.ParseQuality(filename);
        }

        private static Quality GetQualityFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".pdf" => Quality.PDF,
                ".epub" => Quality.EPUB,
                ".mobi" => Quality.MOBI,
                ".azw3" => Quality.AZW3,
                ".cbr" => Quality.CBR,
                ".cbz" => Quality.CBZ,
                ".cbt" => Quality.CBT,
                _ => Quality.Unknown
            };
        }
    }
}
