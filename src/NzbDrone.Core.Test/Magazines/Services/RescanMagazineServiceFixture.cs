using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.MediaFiles;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Magazines.Services;
using NzbDrone.Core.Parser.Model;
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
                Title = "The New Yorker",
                Path = "/magazines/the-new-yorker",
                RootFolderPath = "/magazines",
                WikidataId = "Q123",
                Issn = "1234-5678",
                IssnL = "1234-5678"
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

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetAllMagazines())
                .Returns(new List<Magazine> { _magazine });

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(s => s.GetMissingIssues(_magazine.Id))
                .Returns(new List<MagazineIssue> { _issue });

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.MagazineIssueSearch(_issue.Id, false, false))
                .Returns(Task.FromResult<List<DownloadDecision>>(new List<DownloadDecision>()));

            Mocker.GetMock<IProcessDownloadDecisions>()
                .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns(Task.FromResult(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>())));

            Mocker.GetMock<IMagazineRootFolderService>()
                .Setup(s => s.GetAll())
                .Returns(new List<MagazineRootFolder>());

            Mocker.GetMock<IMagazineFilenameParser>()
                .Setup(s => s.ParseFolderName(It.IsAny<string>()))
                .Returns((string folderName) => new ParsedMagazineIssueInfo
                {
                    MagazineTitle = folderName,
                    NormalizedMagazineTitle = folderName.ToLowerInvariant()
                });
        }

        [Test]
        public void should_search_missing_issues_for_each_requested_magazine()
        {
            Subject.Execute(new RescanMagazineCommand
            {
                MagazineIds = new List<int> { _magazine.Id },
                AddNewIssues = true
            });

            Mocker.GetMock<IMagazineDiskScanService>()
                .Verify(v => v.Scan(It.Is<List<string>>(paths => paths.Count == 1 && paths[0] == "/magazines")), Times.Once());

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(_issue.Id, false, false), Times.Once());

            Mocker.GetMock<IProcessDownloadDecisions>()
                .Verify(v => v.ProcessDecisions(It.IsAny<List<DownloadDecision>>()), Times.Once());
        }

        [Test]
        public void should_skip_missing_issue_search_when_add_new_issues_is_false()
        {
            Subject.Execute(new RescanMagazineCommand
            {
                MagazineIds = new List<int> { _magazine.Id },
                AddNewIssues = false
            });

            Mocker.GetMock<ISearchForReleases>()
                .Verify(v => v.MagazineIssueSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_add_new_magazines_from_configured_root_folders_and_scan_them()
        {
            var rootFolder = new MagazineRootFolder
            {
                Path = "/magazines",
                DefaultQualityProfileId = 2,
                DefaultMetadataProfileId = 3,
                DefaultMonitorOption = MonitorTypes.Missing,
                DefaultTags = new HashSet<int> { 10, 20 }
            };

            var addedMagazine = new Magazine
            {
                Id = 5,
                Title = "Playboy",
                Path = "/magazines/Playboy",
                RootFolderPath = "/magazines",
                AddOptions = new AddMagazineOptions
                {
                    Monitor = MonitorTypes.Missing
                }
            };

            Mocker.GetMock<IMagazineRootFolderService>()
                .Setup(s => s.GetAll())
                .Returns(new List<MagazineRootFolder> { rootFolder });

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists("/magazines"))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.GetDirectories("/magazines"))
                .Returns(new List<string> { "/magazines/Playboy" });

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetAllMagazines())
                .Returns(new List<Magazine>());

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.FindByNormalizedTitle(It.IsAny<string>()))
                .Returns((Magazine)null);

            Mocker.GetMock<IAddMagazineService>()
                .Setup(s => s.AddMagazine(It.IsAny<Magazine>()))
                .Returns(addedMagazine);

            Subject.Execute(new RescanMagazineCommand
            {
                AddNewMagazines = true,
                AddNewIssues = false
            });

            Mocker.GetMock<IAddMagazineService>()
                .Verify(v => v.AddMagazine(It.Is<Magazine>(m =>
                    m.Title == "Playboy" &&
                    m.Path == "/magazines/Playboy" &&
                    m.RootFolderPath == "/magazines" &&
                    m.QualityProfileId == 2 &&
                    m.MetadataProfileId == 3 &&
                    m.Monitored &&
                    m.Tags.SetEquals(new[] { 10, 20 }) &&
                    m.AddOptions.Monitor == MonitorTypes.Missing &&
                    !m.AddOptions.SearchForMissingIssues)), Times.Once());

            Mocker.GetMock<IMagazineDiskScanService>()
                .Verify(v => v.Scan(It.Is<List<string>>(paths => paths.Count == 1 && paths[0] == "/magazines")), Times.Once());

            Mocker.GetMock<IMagazineMonitoredService>()
                .Verify(v => v.SetIssueMonitoredStatus(addedMagazine, MonitorTypes.Missing), Times.Once());
        }

        [Test]
        public void should_backfill_missing_issn_metadata_for_existing_magazines_before_scanning()
        {
            _magazine.WikidataId = "Q564658";
            _magazine.Issn = null;
            _magazine.IssnL = null;
            _magazine.Country = null;
            _magazine.Language = null;

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(s => s.LookupByTitleAsync(_magazine.Title, default))
                .ReturnsAsync(new MagazineAuthorityResult
                {
                    CanonicalTitle = _magazine.Title,
                    WikidataId = "Q564658",
                    Issn = "1432-640X",
                    IssnL = "1432-640X",
                    Country = "Germany",
                    Language = "German"
                });

            Subject.Execute(new RescanMagazineCommand
            {
                MagazineIds = new List<int> { _magazine.Id },
                AddNewIssues = false
            });

            Mocker.GetMock<IMagazineService>()
                .Verify(v => v.UpdateMagazine(It.Is<Magazine>(m =>
                    m.Id == _magazine.Id &&
                    m.Issn == "1432-640X" &&
                    m.IssnL == "1432-640X" &&
                    m.Country == "Germany" &&
                    m.Language == "German")), Times.Once());
        }
    }
}
