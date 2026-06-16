using System.IO.Abstractions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Magazines.MediaFiles
{
    public class LocalMagazineIssue
    {
        public Magazine Magazine { get; set; }
        public MagazineIssue Issue { get; set; }
        public ParsedMagazineIssueInfo ParsedInfo { get; set; }
        public IFileInfo Path { get; set; }
        public QualityModel Quality { get; set; }
    }
}
