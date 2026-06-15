using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.Services;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Magazines.Services
{
    [TestFixture]
    public class DeleteMagazineServiceFixture : CoreTest<DeleteMagazineService>
    {
        private Magazine _magazine;

        [SetUp]
        public void SetUp()
        {
            _magazine = new Magazine
            {
                Id = 1,
                Title = "Time",
                Path = @"C:\Magazines\Time".AsOsAgnostic()
            };

            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetMagazine(_magazine.Id))
                .Returns(_magazine);

            Mocker.GetMock<IDiskProvider>()
                .Setup(s => s.FolderExists(It.IsAny<string>()))
                .Returns(true);
        }

        [Test]
        public void should_delete_files_when_requested_and_folder_is_safe()
        {
            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetAllMagazines())
                .Returns(new List<Magazine>());

            Subject.Execute(new DeleteMagazineCommand
            {
                MagazineId = _magazine.Id,
                DeleteFiles = true
            });

            Mocker.GetMock<IMagazineService>()
                .Verify(v => v.DeleteMagazine(_magazine.Id, true), Times.Once());

            Mocker.GetMock<IRecycleBinProvider>()
                .Verify(v => v.DeleteFolder(_magazine.Path), Times.Once());
        }

        [Test]
        public void should_skip_file_deletion_when_other_magazines_share_the_folder()
        {
            Mocker.GetMock<IMagazineService>()
                .Setup(s => s.GetAllMagazines())
                .Returns(new List<Magazine>
                {
                    new Magazine
                    {
                        Id = 2,
                        Title = "Time for Kids",
                        Path = $"{_magazine.Path}\\Kids".AsOsAgnostic()
                    }
                });

            Subject.Execute(new DeleteMagazineCommand
            {
                MagazineId = _magazine.Id,
                DeleteFiles = true
            });

            Mocker.GetMock<IMagazineService>()
                .Verify(v => v.DeleteMagazine(_magazine.Id, true), Times.Once());

            Mocker.GetMock<IRecycleBinProvider>()
                .Verify(v => v.DeleteFolder(It.IsAny<string>()), Times.Never());
        }
    }
}
