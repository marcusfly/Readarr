using System.Collections.Generic;

namespace Readarr.Api.V3.Magazines
{
    public class MagazineIssueMonitoredResource
    {
        public List<int> IssueIds { get; set; }
        public bool Monitored { get; set; }
    }
}
