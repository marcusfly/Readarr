using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.RootFolderTests
{
    [TestFixture]
    public class AudiobookPathBackfillServiceFixture : CoreTest<AudiobookPathBackfillService>
    {
        private readonly List<RootFolder> _rootFolders = new List<RootFolder>();
        private readonly List<Author> _authors = new List<Author>();
        private readonly List<Author> _updatedAuthors = new List<Author>();

        [SetUp]
        public void Setup()
        {
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__AUDIOBOOKPATH", "/audiobooks");

            _rootFolders.Clear();
            _authors.Clear();
            _updatedAuthors.Clear();

            _rootFolders.Add(new RootFolder
            {
                Path = @"/books".AsOsAgnostic(),
                Name = "Books"
            });

            _rootFolders.Add(new RootFolder
            {
                Path = @"/audiobooks".AsOsAgnostic(),
                Name = "Audiobooks"
            });

            _authors.Add(new Author
            {
                Path = @"/books/Fake Author".AsOsAgnostic(),
                AudiobookPath = null,
                Metadata = new AuthorMetadata
                {
                    Name = "Fake Author",
                    ForeignAuthorId = "fake-author"
                }
            });

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.All())
                .Returns(_rootFolders);

            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>(), It.IsAny<List<RootFolder>>()))
                .Returns<string, List<RootFolder>>((path, folders) => folders.First(x => x.Path.PathEquals(@"/books")).Path);

            Mocker.GetMock<IAuthorService>()
                .Setup(s => s.GetAllAuthors())
                .Returns(_authors);

            Mocker.GetMock<IAuthorService>()
                .Setup(s => s.UpdateAuthor(It.IsAny<Author>()))
                .Returns<Author>(author =>
                {
                    _updatedAuthors.Add(author);
                    return author;
                });
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("READARR__ROOTFOLDER__AUDIOBOOKPATH", null);
        }

        [Test]
        public void should_backfill_missing_audiobook_paths_from_the_configured_root()
        {
            Subject.Handle(new ApplicationStartedEvent());

            _updatedAuthors.Should().ContainSingle();
            _authors.Single().AudiobookPath.Should().Be(@"/audiobooks/Fake Author".AsOsAgnostic());
            Mocker.GetMock<IAuthorService>()
                .Verify(s => s.UpdateAuthor(It.IsAny<Author>()), Times.Once());
        }

        [Test]
        public void should_not_overwrite_existing_audiobook_paths()
        {
            _authors.Single().AudiobookPath = @"/custom/audiobooks/Fake Author".AsOsAgnostic();

            Subject.Handle(new ApplicationStartedEvent());

            _updatedAuthors.Should().BeEmpty();
            Mocker.GetMock<IAuthorService>()
                .Verify(s => s.UpdateAuthor(It.IsAny<Author>()), Times.Never());
        }

        [Test]
        public void should_skip_backfill_when_the_audiobook_root_folder_is_missing()
        {
            _rootFolders.RemoveAll(x => x.Path.PathEquals(@"/audiobooks"));

            Subject.Handle(new ApplicationStartedEvent());

            _updatedAuthors.Should().BeEmpty();
            Mocker.GetMock<IAuthorService>()
                .Verify(s => s.UpdateAuthor(It.IsAny<Author>()), Times.Never());
        }
    }
}
