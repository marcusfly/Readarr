using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class MetadataProviderSelectorFixture : CoreTest<MetadataProviderSelector>
    {
        private Mock<IMetadataProviderV1> _openLibraryProvider;
        private Mock<IMetadataProviderV1> _rreadingGlassesProvider;

        [SetUp]
        public void SetUp()
        {
            _openLibraryProvider = CreateProvider(
                "openlibrary",
                100,
                MetadataProviderCapability.AuthorLookup |
                MetadataProviderCapability.BookLookup |
                MetadataProviderCapability.AuthorSearch |
                MetadataProviderCapability.BookSearch |
                MetadataProviderCapability.EntitySearch |
                MetadataProviderCapability.IsbnSearch |
                MetadataProviderCapability.AsinSearch);

            _rreadingGlassesProvider = CreateProvider(
                "rreading-glasses",
                10,
                MetadataProviderCapability.AuthorLookup |
                MetadataProviderCapability.BookLookup |
                MetadataProviderCapability.AuthorSearch |
                MetadataProviderCapability.BookSearch |
                MetadataProviderCapability.EntitySearch |
                MetadataProviderCapability.IsbnSearch |
                MetadataProviderCapability.AsinSearch |
                MetadataProviderCapability.ChangeTracking);

            Mocker.SetConstant<IEnumerable<IMetadataProviderV1>>(new[]
            {
                _openLibraryProvider.Object,
                _rreadingGlassesProvider.Object
            });
        }

        [Test]
        public void should_route_author_lookup_to_namespaced_identifier_provider()
        {
            var metadataId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "123").ToString();
            var expected = new Author { Metadata = new AuthorMetadata { ForeignAuthorId = metadataId } };

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataProvider)
                .Returns("openlibrary");

            _rreadingGlassesProvider
                .Setup(x => x.GetAuthorInfo(metadataId, false))
                .Returns(expected);

            var result = Subject.GetAuthorInfo(metadataId, false);

            result.Should().BeSameAs(expected);
            _rreadingGlassesProvider.Verify(x => x.GetAuthorInfo(metadataId, false), Times.Once);
            _openLibraryProvider.Verify(x => x.GetAuthorInfo(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void should_route_search_to_active_provider()
        {
            var expected = new List<Book> { new Book { ForeignBookId = "book-1" } };
            var metadataId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "123").ToString();
            var hydratedMetadata = new AuthorMetadata { ForeignAuthorId = metadataId, Name = "Terry Pratchett" };
            var hydratedBook = new Book
            {
                ForeignBookId = "book-1",
                AuthorMetadata = hydratedMetadata,
                Author = new Author
                {
                    Metadata = hydratedMetadata,
                    CleanName = "terrypratchett"
                },
                Editions = new List<Edition> { new Edition { ForeignEditionId = "edition-1", Monitored = true } }
            };

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataProvider)
                .Returns("rreading-glasses");

            _rreadingGlassesProvider
                .Setup(x => x.SearchForNewBook("guards", "pratchett", true))
                .Returns(expected);

            _rreadingGlassesProvider
                .Setup(x => x.GetBookInfo("book-1"))
                .Returns(Tuple.Create(metadataId, hydratedBook, new List<AuthorMetadata> { hydratedMetadata }));

            var result = Subject.SearchForNewBook("guards", "pratchett");

            result.Should().BeSameAs(expected);
            result.Single().AuthorMetadata.Value.ForeignAuthorId.Should().Be(metadataId);
            result.Single().Author.Value.Metadata.Value.Name.Should().Be("Terry Pratchett");
            _rreadingGlassesProvider.Verify(x => x.SearchForNewBook("guards", "pratchett", true), Times.Once);
            _rreadingGlassesProvider.Verify(x => x.GetBookInfo("book-1"), Times.Once);
            _openLibraryProvider.Verify(x => x.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void should_fall_back_to_provider_with_change_tracking()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataProvider)
                .Returns("openlibrary");

            var changedAuthors = new HashSet<string>
            {
                MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "1").ToString()
            };

            _rreadingGlassesProvider
                .Setup(x => x.GetChangedAuthors(It.IsAny<DateTime>()))
                .Returns(changedAuthors);

            var result = Subject.GetChangedAuthors(new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

            result.Should().BeSameAs(changedAuthors);
            _rreadingGlassesProvider.Verify(x => x.GetChangedAuthors(It.IsAny<DateTime>()), Times.Once);
            _openLibraryProvider.Verify(x => x.GetChangedAuthors(It.IsAny<DateTime>()), Times.Never);
        }

        private Mock<IMetadataProviderV1> CreateProvider(string providerKey, int priority, MetadataProviderCapability capabilities)
        {
            var provider = new Mock<IMetadataProviderV1>();

            provider.SetupGet(x => x.Descriptor)
                .Returns(new MetadataProviderDescriptor(providerKey, providerKey, priority, capabilities));

            provider.Setup(x => x.SupportsIdentifier(It.IsAny<MetadataIdentifier>()))
                .Returns<MetadataIdentifier>(identifier => identifier.Provider == providerKey);

            return provider;
        }
    }
}
