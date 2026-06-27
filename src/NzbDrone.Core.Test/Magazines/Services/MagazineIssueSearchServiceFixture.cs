using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.Services;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Services
{
    [TestFixture]
    public class MagazineIssueSearchServiceFixture : CoreTest<MagazineIssueSearchService>
    {
        private Magazine _magazine;
        private MagazineIssue _issue;

        [SetUp]
        public void SetUp()
        {
            _magazine = new Magazine
            {
                Id = 1,
                Title = "National Geographic",
                AddOptions = new AddMagazineOptions
                {
                    Monitor = MonitorTypes.All
                }
            };

            _issue = new MagazineIssue
            {
                Id = 11,
                MagazineId = _magazine.Id,
                Magazine = _magazine,
                IssueYear = 2024,
                IssueMonth = 6,
                IssueDay = 15,
                ReleaseTitle = "National Geographic 2024-06-15"
            };

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(s => s.GetIssue(_issue.Id))
                .Returns(_issue);

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(s => s.GetMissingIssues(_magazine.Id))
                .Returns(new List<MagazineIssue> { _issue });

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(s => s.GetIssuesByMagazine(_magazine.Id))
                .Returns(new List<MagazineIssue>());

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetMagazine(_magazine.Id))
                .Returns(_magazine);

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.MagazineIssueSearch(_issue.Id, false, false))
                .Returns(Task.FromResult<List<DownloadDecision>>(new List<DownloadDecision>()));
        }

        [Test]
        public void should_search_requested_issue_ids()
        {
            Subject.Execute(new MagazineIssueSearchCommand(new List<int> { _issue.Id }));

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(_issue.Id, false, false), Times.Once());
        }

        [Test]
        public void should_search_missing_issues_for_a_magazine()
        {
            Subject.Execute(new MissingMagazineIssueSearchCommand(_magazine.Id));

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(_issue.Id, false, false), Times.Once());
        }

        [Test]
        public void should_support_user_invoked_magazine_search_without_issue_rows()
        {
            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.MagazineSearch(_magazine.Id, true, false))
                .Returns(Task.FromResult<List<DownloadDecision>>(new List<DownloadDecision>()));

            Subject.Execute(new MagazineSearchCommand(_magazine.Id));

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineSearch(_magazine.Id, true, false), Times.Once());
        }

        [Test]
        public void should_persist_discovered_issues_during_title_search()
        {
            var discoveredIssue = new MagazineIssue
            {
                MagazineId = _magazine.Id,
                IssueYear = 2026,
                IssueMonth = 2,
                ReleaseTitle = "Playboy 2026-02",
                Monitored = true
            };

            var remoteIssue = new RemoteMagazineIssue
            {
                Magazine = _magazine,
                Issue = discoveredIssue,
                DownloadAllowed = true,
                Release = new ReleaseInfo
                {
                    Title = "Playboy No 02 2026",
                    Indexer = "TestIndexer"
                },
                ParsedMagazineIssueInfo = new ParsedMagazineIssueInfo
                {
                    MagazineTitle = _magazine.Title,
                    IssueYear = 2026,
                    IssueMonth = 2
                }
            };

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(s => s.UpsertIssue(It.IsAny<MagazineIssue>()))
                .Returns<MagazineIssue>((issue) =>
                {
                    issue.Id = 42;
                    return issue;
                });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.MagazineSearch(_magazine.Id, true, false))
                .Returns(Task.FromResult(new List<DownloadDecision>
                {
                    new DownloadDecision(remoteIssue)
                }));

            Subject.Execute(new MagazineSearchCommand(_magazine.Id));

            Mocker.GetMock<IMagazineIssueService>()
                .Verify(v => v.UpsertIssue(It.Is<MagazineIssue>(issue =>
                    issue.MagazineId == _magazine.Id &&
                    issue.IssueYear == 2026 &&
                    issue.IssueMonth == 2)), Times.Once());

            Mocker.GetMock<IMagazineMonitoredService>()
                .Verify(v => v.SetIssueMonitoredStatus(_magazine, MonitorTypes.All), Times.Once());

            Mocker.GetMock<IDownloadService>()
                .Verify(v => v.DownloadReport(It.IsAny<RemoteBook>(), null), Times.Once());

            remoteIssue.Issue.Id.Should().Be(42);
        }
    }
}
