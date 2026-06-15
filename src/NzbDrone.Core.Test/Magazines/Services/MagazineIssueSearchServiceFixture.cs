using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.Services;
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
                Title = "National Geographic"
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

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.MagazineIssueSearch(_issue.Id, false, false))
                .Returns(Task.FromResult<List<DownloadDecision>>(new List<DownloadDecision>()));

            Mocker.GetMock<IProcessDownloadDecisions>()
                .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));
        }

        [Test]
        public void should_search_requested_issue_ids()
        {
            Subject.Execute(new MagazineIssueSearchCommand(new List<int> { _issue.Id }));

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(_issue.Id, false, false), Times.Once());

            Mocker.GetMock<IProcessDownloadDecisions>()
                .Verify(v => v.ProcessDecisions(It.IsAny<List<DownloadDecision>>()), Times.Once());
        }

        [Test]
        public void should_search_missing_issues_for_a_magazine()
        {
            Subject.Execute(new MissingMagazineIssueSearchCommand(_magazine.Id));

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(_issue.Id, false, false), Times.Once());
        }
    }
}
