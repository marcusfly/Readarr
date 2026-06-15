using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Magazines.Services;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Magazines.Services
{
    [TestFixture]
    public class MagazineImportServiceFixture : FileSystemTest<MagazineImportService>
    {
        private MagazineIssue _issue;
        private string _rootFolder;
        private string _pdfFile;
        private string _cbzFile;
        private string _imageFile;

        [SetUp]
        public void SetUp()
        {
            _rootFolder = @"C:\Magazines".AsOsAgnostic();
            _pdfFile = Path.Combine(_rootFolder, "wired-2024-06.pdf");
            _cbzFile = Path.Combine(_rootFolder, "wired-2024-07.cbz");
            _imageFile = Path.Combine(_rootFolder, "cover.jpg");

            FileSystem.AddDirectory(_rootFolder);
            FileSystem.AddFile(_pdfFile, new MockFileData("pdf"));
            FileSystem.AddFile(_cbzFile, new MockFileData("cbz"));
            FileSystem.AddFile(_imageFile, new MockFileData("jpg"));

            _issue = new MagazineIssue
            {
                Id = 42,
                MagazineId = 7,
                IssueYear = 2024,
                IssueMonth = 6,
                IssueDay = null,
                Volume = "15",
                IssueNumber = "6",
                ReleaseTitle = "Wired 2024-06"
            };

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(s => s.GetIssue(_issue.Id))
                .Returns(_issue);

            Mocker.GetMock<IMagazineIssueFileService>()
                .Setup(s => s.GetFilesForIssue(_issue.Id))
                .Returns(new List<MagazineIssueFile>());
        }

        [Test]
        public void should_list_only_known_magazine_files_and_include_issue_context()
        {
            var result = Subject.GetMediaFiles(_rootFolder, _issue);

            result.Should().HaveCount(2);
            result.Select(x => x.Path).Should().ContainInOrder(_pdfFile, _cbzFile);
            result.Should().OnlyContain(x => x.MagazineIssueId == _issue.Id);
            result.Should().OnlyContain(x => x.MagazineId == _issue.MagazineId);
            result.Should().OnlyContain(x => x.IssueYear == _issue.IssueYear);
            result.Should().OnlyContain(x => x.Quality != null && x.Quality.Quality != Quality.Unknown);
        }

        [Test]
        public void should_import_issue_file_and_publish_event()
        {
            var imported = Subject.UpdateItems(new List<MagazineImportItem>
            {
                new MagazineImportItem
                {
                    Path = _pdfFile,
                    MagazineIssueId = _issue.Id
                }
            });

            imported.Should().ContainSingle(x => x.Path == _pdfFile);

            Mocker.GetMock<IMagazineIssueFileService>()
                .Verify(v => v.Add(It.Is<MagazineIssueFile>(f =>
                    f.Path == _pdfFile &&
                    f.MagazineIssueId == _issue.Id &&
                    f.MagazineId == _issue.MagazineId &&
                    f.Quality.Quality == Quality.PDF)), Times.Once());

            Mocker.GetMock<IEventAggregator>()
                .Verify(v => v.PublishEvent(It.Is<MagazineIssueFileImportedEvent>(e =>
                    e.MagazineIssue.Id == _issue.Id &&
                    e.MagazineIssueFile.Path == _pdfFile)), Times.Once());
        }

        [Test]
        public void should_update_existing_issue_file_when_the_same_path_is_imported_again()
        {
            var existing = new MagazineIssueFile
            {
                Id = 99,
                MagazineIssueId = _issue.Id,
                MagazineId = _issue.MagazineId,
                Path = _cbzFile,
                Size = 1,
                DateAdded = new System.DateTime(2024, 1, 1),
                Quality = new QualityModel { Quality = Quality.Unknown }
            };

            Mocker.GetMock<IMagazineIssueFileService>()
                .Setup(s => s.GetFilesForIssue(_issue.Id))
                .Returns(new List<MagazineIssueFile> { existing });

            Subject.UpdateItems(new List<MagazineImportItem>
            {
                new MagazineImportItem
                {
                    Path = _cbzFile,
                    MagazineIssueId = _issue.Id
                }
            });

            Mocker.GetMock<IMagazineIssueFileService>()
                .Verify(v => v.Update(It.Is<MagazineIssueFile>(f =>
                    f.Id == existing.Id &&
                    f.Path == _cbzFile &&
                    f.Size == 3 &&
                    f.Quality.Quality == Quality.CBZ)), Times.Once());

            Mocker.GetMock<IMagazineIssueFileService>()
                .Verify(v => v.Add(It.IsAny<MagazineIssueFile>()), Times.Never());
        }
    }
}
