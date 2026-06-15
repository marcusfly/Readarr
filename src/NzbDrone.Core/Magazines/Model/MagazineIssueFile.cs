using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Magazines
{
    public class MagazineIssueFile : ModelBase
    {
        public int MagazineIssueId { get; set; }
        public int MagazineId { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public DateTime DateAdded { get; set; }
        public QualityModel Quality { get; set; }
        public MediaInfoModel MediaInfo { get; set; }

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id, Path);
        }
    }
}
