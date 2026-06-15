using System.Collections.Generic;
using System.IO;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.Services;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Magazines.Services
{
    [TestFixture]
    public class MoveMagazineServiceFixture : CoreTest<MoveMagazineService>
    {
        private Magazine _magazine;
        private MagazineIssueFile _firstIssueFile;
        private MagazineIssueFile _secondIssueFile;
        private MoveMagazineCommand _command;

        [SetUp]
        public void SetUp()
        {
            _magazine = new Magazine
            {
                Id = 1,
                Title = "Wired",
                Path = @"C:\Magazines\Wired".AsOsAgnostic()
            };

            _firstIssueFile = new MagazineIssueFile
            {
                Id = 101,
                MagazineId = _magazine.Id,
                Path = Path.Combine(_magazine.Path, "2024-06", "issue1.pdf")
            };

            _secondIssueFile = new MagazineIssueFile
            {
                Id = 102,
                MagazineId = _magazine.Id,
                Path = Path.Combine(_magazine.Path, "archive", "issue2.epub")
            };

            _command = new MoveMagazineCommand
            {
                MagazineId = _magazine.Id,
                SourcePath = _magazine.Path,
                DestinationPath = @"D:\Library\Wired".AsOsAgnostic()
            };

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetMagazine(_magazine.Id))
                .Returns(_magazine);

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.UpdateMagazine(It.IsAny<Magazine>()))
                .Returns<Magazine>(m => m);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(true);

            Mocker.GetMock<IMagazineIssueFileService>()
                .Setup(s => s.GetFilesByMagazine(_magazine.Id))
                .Returns(new List<MagazineIssueFile> { _firstIssueFile, _secondIssueFile });
        }

        [Test]
        public void should_move_folder_and_update_issue_file_paths()
        {
            Subject.Execute(_command);

            Mocker.GetMock<IDiskTransferService>()
                .Verify(v => v.TransferFolder(_command.SourcePath, _command.DestinationPath, TransferMode.Move), Times.Once());

            Mocker.GetMock<IMagazineService>()
                .Verify(v => v.UpdateMagazine(It.Is<Magazine>(m => m.Path == _command.DestinationPath)), Times.Once());

            Mocker.GetMock<IMagazineIssueFileService>()
                .Verify(v => v.Update(It.Is<MagazineIssueFile>(f => f.Path == Path.Combine(_command.DestinationPath, "2024-06", "issue1.pdf"))), Times.Once());

            Mocker.GetMock<IMagazineIssueFileService>()
                .Verify(v => v.Update(It.Is<MagazineIssueFile>(f => f.Path == Path.Combine(_command.DestinationPath, "archive", "issue2.epub"))), Times.Once());
        }

        [Test]
        public void should_skip_move_when_source_folder_is_missing()
        {
            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(false);

            Subject.Execute(_command);

            Mocker.GetMock<IDiskTransferService>()
                .Verify(v => v.TransferFolder(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Move), Times.Never());

            Mocker.GetMock<IMagazineService>()
                .Verify(v => v.UpdateMagazine(It.IsAny<Magazine>()), Times.Never());
        }
    }
}
