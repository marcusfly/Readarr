using System;
using System.Collections.Generic;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Qualities;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    public class MagazineIssueResource : RestResource
    {
        public int MagazineId { get; set; }
        public int IssueYear { get; set; }
        public int IssueMonth { get; set; }
        public int? IssueDay { get; set; }
        public string Volume { get; set; }
        public string IssueNumber { get; set; }
        public string ReleaseTitle { get; set; }
        public bool Monitored { get; set; }
        public bool HasFile { get; set; }
        public DateTime Added { get; set; }
        public QualityModel Quality { get; set; }
        public List<MediaCover> Images { get; set; }
    }
}
