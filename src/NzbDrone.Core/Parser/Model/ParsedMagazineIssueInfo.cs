using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser.Model
{
    public class ParsedMagazineIssueInfo
    {
        public string MagazineTitle { get; set; }
        public string NormalizedMagazineTitle { get; set; }
        public int IssueYear { get; set; }
        public int IssueMonth { get; set; }
        public int? IssueDay { get; set; }
        public string Volume { get; set; }
        public string IssueNumber { get; set; }
        public QualityModel Quality { get; set; }
        public string ReleaseTitle { get; set; }
        public float Confidence { get; set; } = 1.0f;
    }
}
