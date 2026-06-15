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
    public class RescanMagazineServiceFixture : CoreTest<RescanMagazineService>
    {
        private Magazine _magazine;
        private MagazineIssue _issue;

        [SetUp]
        public void SetUp()
        {
            _magazine = new Magazine
            {
                Id = 1,
                Title = "The New Yorker"
            };

            _issue = new MagazineIssue
            {
                Id = 17,
                MagazineId = _magazine.Id,
                Magazine = _magazine,
                IssueYear = 2024,
                IssueMonth = 6,
                IssueDay = null,
                ReleaseTitle = "The New Yorker 2024-06"
            };

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetMagazine(_magazine.Id))
                .Returns(_magazine);

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
        public void should_search_missing_issues_for_each_requested_magazine()
        {
            Subject.Execute(new RescanMagazineCommand
            {
                MagazineIds = new List<int> { _magazine.Id },
                AddNewIssues = false
            });

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(_issue.Id, false, false), Times.Once());

            Mocker.GetMock<IProcessDownloadDecisions>()
                .Verify(v => v.ProcessDecisions(It.IsAny<List<DownloadDecision>>()), Times.Once());
        }
    }
}
