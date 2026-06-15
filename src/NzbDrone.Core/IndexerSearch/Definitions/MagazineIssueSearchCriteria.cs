using System.Collections.Generic;
using NzbDrone.Core.Magazines;

namespace NzbDrone.Core.IndexerSearch.Definitions
{
    public class MagazineIssueSearchCriteria : SearchCriteriaBase
    {
        public Magazine Magazine { get; set; }
        public MagazineIssue Issue { get; set; }
        public string MagazineTitle { get; set; }
        public int IssueYear { get; set; }
        public int IssueMonth { get; set; }
        public int? IssueDay { get; set; }

        public string IssueQuery => $"{SearchCriteriaBase.GetQueryTitle(MagazineTitle)} {IssueYear:D4}-{IssueMonth:D2}{(IssueDay.HasValue ? $"-{IssueDay.Value:D2}" : string.Empty)}";

        // Categories: 7000 (books parent) and 7010 (misc books)
        public override int[] IndexerCategories => new[] { 7000, 7010 };
        public override HashSet<int> Tags => Magazine?.Tags;

        public override string ToString() =>
            $"[{MagazineTitle} {IssueYear:D4}-{IssueMonth:D2}{(IssueDay.HasValue ? $"-{IssueDay.Value:D2}" : string.Empty)}]";
    }
}
