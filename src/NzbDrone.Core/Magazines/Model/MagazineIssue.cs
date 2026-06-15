using System;
using System.Collections.Generic;
using Equ;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Magazines
{
    public class MagazineIssue : Entity<MagazineIssue>
    {
        public int MagazineId { get; set; }
        public int IssueYear { get; set; }
        public int IssueMonth { get; set; }
        public int? IssueDay { get; set; }
        public string Volume { get; set; }
        public string IssueNumber { get; set; }
        public string ReleaseTitle { get; set; }
        public bool Monitored { get; set; }
        public DateTime Added { get; set; }
        public DateTime? LastSearchTime { get; set; }

        // Lazy-loaded
        [MemberwiseEqualityIgnore]
        public LazyLoaded<Magazine> Magazine { get; set; }

        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<MagazineIssueFile>> IssueFiles { get; set; }
    }

    // Issue identity for deduplication: (MagazineId, IssueYear, IssueMonth, IssueDay)
    public static class MagazineIssueIdentity
    {
        public static (int MagazineId, int Year, int Month, int? Day) Of(MagazineIssue issue)
            => (issue.MagazineId, issue.IssueYear, issue.IssueMonth, issue.IssueDay);
    }
}
