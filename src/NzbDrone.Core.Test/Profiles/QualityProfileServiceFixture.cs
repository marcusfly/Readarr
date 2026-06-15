using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.ContentTypes;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Profiles
{
    [TestFixture]

    public class QualityProfileServiceFixture : CoreTest<QualityProfileService>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<ICustomFormatService>()
                .Setup(s => s.All())
                .Returns(new List<CustomFormat>());
        }

        [Test]
        public void init_should_add_default_profiles()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Insert(It.IsAny<QualityProfile>()), Times.Exactly(3));

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Insert(It.Is<QualityProfile>(p =>
                    p.Name == "Both" &&
                    p.GetAllowedContentTypes() == (LibraryContentType.Book | LibraryContentType.Audiobook))), Times.Once());

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Insert(It.Is<QualityProfile>(p =>
                    p.Name == "eBook" &&
                    p.GetAllowedContentTypes() == LibraryContentType.Book)), Times.Once());

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Insert(It.Is<QualityProfile>(p =>
                    p.Name == "Audiobook" &&
                    p.GetAllowedContentTypes() == LibraryContentType.Audiobook)), Times.Once());
        }

        [Test]

        public void init_should_add_missing_default_profiles_without_duplicating_existing_profiles()
        {
            Mocker.GetMock<IProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile>
                  {
                      Subject.GetDefaultProfile("Both", Quality.AZW3, Quality.EPUB, Quality.MP3),
                      Subject.GetDefaultProfile("eBook", Quality.MOBI, Quality.MOBI, Quality.EPUB, Quality.AZW3)
                  });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Insert(It.Is<QualityProfile>(p => p.Name == "Audiobook")), Times.Once());

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Insert(It.Is<QualityProfile>(p => p.Name == "Both" || p.Name == "eBook")), Times.Never());
        }

        [Test]
        public void init_should_rename_legacy_spoken_profile_when_it_is_audio_only()
        {
            var spoken = Subject.GetDefaultProfile("Spoken", Quality.MP3, Quality.UnknownAudio, Quality.MP3, Quality.M4B, Quality.FLAC);

            Mocker.GetMock<IProfileRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<QualityProfile> { spoken });

            Subject.Handle(new ApplicationStartedEvent());

            spoken.Name.Should().Be("Audiobook");

            Mocker.GetMock<IProfileRepository>()
                .Verify(v => v.Update(It.Is<QualityProfile>(p => p.Name == "Audiobook")), Times.Once());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_author()
        {
            var profile = Builder<QualityProfile>.CreateNew()
                                          .With(p => p.Id = 2)
                                          .Build();

            var authorList = Builder<Author>.CreateListOfSize(3)
                                            .Random(1)
                                            .With(c => c.QualityProfileId = profile.Id)
                                            .Build().ToList();

            var importLists = Builder<ImportListDefinition>.CreateListOfSize(2)
                .All()
                .With(c => c.ProfileId = 1)
                .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .All()
                .With(f => f.DefaultQualityProfileId = 1)
                .BuildList();

            Mocker.GetMock<IAuthorService>().Setup(c => c.GetAllAuthors()).Returns(authorList);
            Mocker.GetMock<IImportListFactory>().Setup(c => c.All()).Returns(importLists);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);
            Mocker.GetMock<IProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(profile.Id));

            Mocker.GetMock<IProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_import_list()
        {
            var profile = Builder<QualityProfile>.CreateNew()
                .With(p => p.Id = 2)
                .Build();

            var authorList = Builder<Author>.CreateListOfSize(3)
                .All()
                .With(c => c.QualityProfileId = 1)
                .Build().ToList();

            var importLists = Builder<ImportListDefinition>.CreateListOfSize(2)
                .Random(1)
                .With(c => c.ProfileId = profile.Id)
                .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .All()
                .With(f => f.DefaultQualityProfileId = 1)
                .BuildList();

            Mocker.GetMock<IAuthorService>().Setup(c => c.GetAllAuthors()).Returns(authorList);
            Mocker.GetMock<IImportListFactory>().Setup(c => c.All()).Returns(importLists);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);
            Mocker.GetMock<IProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(profile.Id));

            Mocker.GetMock<IProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_not_be_able_to_delete_profile_if_assigned_to_root_folder()
        {
            var profile = Builder<QualityProfile>.CreateNew()
                .With(p => p.Id = 2)
                .Build();

            var authorList = Builder<Author>.CreateListOfSize(3)
                .All()
                .With(c => c.QualityProfileId = 1)
                .Build().ToList();

            var importLists = Builder<ImportListDefinition>.CreateListOfSize(2)
                .All()
                .With(c => c.ProfileId = 1)
                .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .Random(1)
                .With(f => f.DefaultQualityProfileId = profile.Id)
                .BuildList();

            Mocker.GetMock<IAuthorService>().Setup(c => c.GetAllAuthors()).Returns(authorList);
            Mocker.GetMock<IImportListFactory>().Setup(c => c.All()).Returns(importLists);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);
            Mocker.GetMock<IProfileRepository>().Setup(c => c.Get(profile.Id)).Returns(profile);

            Assert.Throws<QualityProfileInUseException>(() => Subject.Delete(profile.Id));

            Mocker.GetMock<IProfileRepository>().Verify(c => c.Delete(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void should_delete_profile_if_not_assigned_to_author_import_list_or_root_folder()
        {
            var authorList = Builder<Author>.CreateListOfSize(3)
                                            .All()
                                            .With(c => c.QualityProfileId = 2)
                                            .Build().ToList();

            var importLists = Builder<ImportListDefinition>.CreateListOfSize(2)
                .All()
                .With(c => c.ProfileId = 2)
                .Build().ToList();

            var rootFolders = Builder<RootFolder>.CreateListOfSize(2)
                .All()
                .With(f => f.DefaultQualityProfileId = 2)
                .BuildList();

            Mocker.GetMock<IAuthorService>().Setup(c => c.GetAllAuthors()).Returns(authorList);
            Mocker.GetMock<IImportListFactory>().Setup(c => c.All()).Returns(importLists);
            Mocker.GetMock<IRootFolderService>().Setup(c => c.All()).Returns(rootFolders);

            Subject.Delete(1);

            Mocker.GetMock<IProfileRepository>().Verify(c => c.Delete(1), Times.Once());
        }
    }
}
