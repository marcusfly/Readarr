using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
    public class MagazineIssueSearchDefinitionFixture : CoreTest<MagazineIssueSearchCriteria>
    {
        [Test]
        public void should_build_issue_query_with_day()
        {
            Subject.Magazine = new Magazine
            {
                Title = "National Geographic"
            };
            Subject.MagazineTitle = Subject.Magazine.Title;
            Subject.IssueYear = 2024;
            Subject.IssueMonth = 6;
            Subject.IssueDay = 15;

            Subject.IssueQuery.Should().Be("National+Geographic 2024-06-15");
        }

        [Test]
        public void should_build_issue_query_without_day()
        {
            Subject.Magazine = new Magazine
            {
                Title = "Popular Science"
            };
            Subject.MagazineTitle = Subject.Magazine.Title;
            Subject.IssueYear = 2024;
            Subject.IssueMonth = 6;

            Subject.IssueQuery.Should().Be("Popular+Science 2024-06");
        }
    }
}
