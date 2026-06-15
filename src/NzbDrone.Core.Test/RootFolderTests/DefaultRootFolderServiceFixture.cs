using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.RootFolderTests
{
    [TestFixture]
    public class DefaultRootFolderServiceFixture : CoreTest<DefaultRootFolderService>
    {
        private List<RootFolder> _existingRootFolders;
        private List<RootFolder> _addedRootFolders;

        [SetUp]
        public void Setup()
        {
            _existingRootFolders = new List<RootFolder>();
            _addedRootFolders = new List<RootFolder>();

            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__PATH", null);
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__AUDIOBOOKPATH", null);

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.All())
                .Returns(_existingRootFolders);

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.Add(It.IsAny<RootFolder>()))
                .Returns<RootFolder>(rootFolder =>
                {
                    _addedRootFolders.Add(rootFolder);
                    return rootFolder;
                });

            Mocker.GetMock<IMetadataProfileService>()
                .Setup(s => s.All())
                .Returns(new List<MetadataProfile>
                {
                    new MetadataProfile { Id = 2, Name = "Standard" }
                });

            Mocker.GetMock<IQualityProfileService>()
                .Setup(s => s.All())
                .Returns(new List<QualityProfile>
                {
                    new QualityProfile { Id = 3, Name = "Both" }
                });
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__PATH", null);
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__AUDIOBOOKPATH", null);
        }

        [Test]
        public void should_add_default_book_and_audiobook_root_folders_on_startup()
        {
            Subject.Handle(new ApplicationStartedEvent());

            _addedRootFolders.Should().HaveCount(2);
            _addedRootFolders.Should().Contain(x => x.Path == "/books" && x.Name == "books");
            _addedRootFolders.Should().Contain(x => x.Path == "/audiobooks" && x.Name == "audiobooks");
            _addedRootFolders.Should().OnlyContain(x => x.DefaultMetadataProfileId == 2 && x.DefaultQualityProfileId == 3);
        }

        [Test]
        public void should_add_missing_audiobook_root_folder_when_book_root_folder_already_exists()
        {
            _existingRootFolders.Add(new RootFolder { Path = "/books" });

            Subject.Handle(new ApplicationStartedEvent());

            _addedRootFolders.Should().ContainSingle();
            _addedRootFolders.Should().Contain(x => x.Path == "/audiobooks");
        }

        [Test]
        public void should_use_configured_book_and_audiobook_root_folder_paths()
        {
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__PATH", "/library/books");
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__AUDIOBOOKPATH", "/library/audiobooks");

            Subject.Handle(new ApplicationStartedEvent());

            _addedRootFolders.Should().HaveCount(2);
            _addedRootFolders.Should().Contain(x => x.Path == "/library/books");
            _addedRootFolders.Should().Contain(x => x.Path == "/library/audiobooks");
        }
    }
}
