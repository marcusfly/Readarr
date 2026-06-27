using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Services
{
    [TestFixture]
    public class MagazineParsingServiceFixture : CoreTest<MagazineParsingService>
    {
        [Test]
        public void should_create_transient_issue_for_user_invoked_search_when_no_issue_exists()
        {
            var magazine = new Magazine
            {
                Id = 5,
                Title = "Playboy"
            };

            var parsed = new ParsedMagazineIssueInfo
            {
                MagazineTitle = "Playboy",
                NormalizedMagazineTitle = "playboy",
                IssueYear = 2026,
                IssueMonth = 6,
                ReleaseTitle = "Playboy 2026-06"
            };

            Mocker.GetMock<IMagazineService>()
                .Setup(x => x.FindByNormalizedTitle("playboy"))
                .Returns(magazine);

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(x => x.GetIssuesByMagazine(magazine.Id))
                .Returns(new List<MagazineIssue>());

            var criteria = new MagazineIssueSearchCriteria
            {
                Magazine = magazine,
                MagazineTitle = "Playboy",
                UserInvokedSearch = true
            };

            var result = Subject.Map(parsed, criteria);

            result.Should().NotBeNull();
            result.Magazine.Should().BeSameAs(magazine);
            result.Issue.Should().NotBeNull();
            result.Issue.Id.Should().Be(0);
            result.Issue.MagazineId.Should().Be(magazine.Id);
            result.Issue.IssueYear.Should().Be(2026);
            result.Issue.IssueMonth.Should().Be(6);
            result.Issue.ReleaseTitle.Should().Be("Playboy 2026-06");
            result.Issue.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_not_create_transient_issue_for_automatic_search_without_existing_issue()
        {
            var magazine = new Magazine
            {
                Id = 5,
                Title = "Playboy"
            };

            var parsed = new ParsedMagazineIssueInfo
            {
                MagazineTitle = "Playboy",
                NormalizedMagazineTitle = "playboy",
                IssueYear = 2026,
                IssueMonth = 6,
                ReleaseTitle = "Playboy 2026-06"
            };

            Mocker.GetMock<IMagazineService>()
                .Setup(x => x.FindByNormalizedTitle("playboy"))
                .Returns(magazine);

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(x => x.GetIssuesByMagazine(magazine.Id))
                .Returns(new List<MagazineIssue>());

            var criteria = new MagazineIssueSearchCriteria
            {
                Magazine = magazine,
                MagazineTitle = "Playboy"
            };

            var result = Subject.Map(parsed, criteria);

            result.Should().NotBeNull();
            result.Magazine.Should().BeSameAs(magazine);
            result.Issue.Should().BeNull();
        }
    }
}
