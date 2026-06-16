using NzbDrone.Core.ContentTypes;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.TagExtraction
{
    public class FileTagResult
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public string Narrator { get; set; }
        public string Series { get; set; }
        public string SeriesIndex { get; set; }
        public string Publisher { get; set; }
        public int? Year { get; set; }
        public string Isbn { get; set; }
        public string Asin { get; set; }
        public QualityModel Quality { get; set; }
        public LibraryContentType ContentType { get; set; }
        public bool HasChapters { get; set; }
        public int? ChapterCount { get; set; }
        public long? DurationMs { get; set; }
        public ParsedTrackInfo ParsedTrackInfo { get; set; }
    }
}
