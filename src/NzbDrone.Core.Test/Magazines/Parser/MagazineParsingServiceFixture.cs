using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Parser
{
    [TestFixture]
    public class MagazineParsingServiceFixture : CoreTest<MagazineParsingService>
    {
        private Magazine _magazine;
        private MagazineIssue _issue;

        [SetUp]
        public void SetUp()
        {
            _magazine = new Magazine
            {
                Id = 11,
                Title = "Motor Trend",
                NormalizedTitle = "motor trend"
            };

            _issue = new MagazineIssue
            {
                Id = 17,
                MagazineId = _magazine.Id,
                IssueYear = 2024,
                IssueMonth = 3,
                IssueDay = 1,
                ReleaseTitle = "Motor Trend 2024-03-01"
            };

            Mocker.GetMock<IMagazineService>()
                .Setup(x => x.FindByNormalizedTitle(_magazine.NormalizedTitle))
                .Returns(_magazine);

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(x => x.GetIssuesByMagazine(_magazine.Id))
                .Returns(new List<MagazineIssue> { _issue });
        }

        [Test]
        public void should_match_issue_by_year_month_day()
        {
            var result = Subject.Map(new ParsedMagazineIssueInfo
            {
                MagazineTitle = _magazine.Title,
                NormalizedMagazineTitle = _magazine.NormalizedTitle,
                IssueYear = 2024,
                IssueMonth = 3,
                IssueDay = 1
            });

            result.Magazine.Should().Be(_magazine);
            result.Issue.Should().Be(_issue);
        }

        [Test]
        public void should_return_remote_issue_with_null_magazine_when_unknown()
        {
            var result = Subject.Map(new ParsedMagazineIssueInfo
            {
                MagazineTitle = "Unknown",
                NormalizedMagazineTitle = "unknown",
                IssueYear = 2024,
                IssueMonth = 3
            });

            result.Magazine.Should().BeNull();
            result.Issue.Should().BeNull();
        }

        [Test]
        public void should_use_search_criteria_directly_for_magazine_searches()
        {
            var criteria = new MagazineIssueSearchCriteria
            {
                Magazine = _magazine,
                Issue = _issue
            };

            var result = Subject.Map(new ParsedMagazineIssueInfo
            {
                MagazineTitle = "Mismatch",
                NormalizedMagazineTitle = "mismatch",
                IssueYear = 1999,
                IssueMonth = 1
            }, criteria);

            result.Magazine.Should().Be(_magazine);
            result.Issue.Should().Be(_issue);
        }
    }
}
