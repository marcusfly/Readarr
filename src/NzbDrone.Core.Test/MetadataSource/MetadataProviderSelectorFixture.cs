using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

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
                MetadataProviderCapability.IsbnSearch);

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

            Mocker.GetMock<IMagazineService>()
                .Setup(x => x.FindByNormalizedTitle(It.IsAny<string>()))
                .Returns((Magazine)null);

            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MagazineAuthorityResult)null);

            _openLibraryProvider
                .Setup(x => x.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new List<Book>());

            _rreadingGlassesProvider
                .Setup(x => x.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Returns(new List<Book>());
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

            result.Should().HaveCount(1);
            result.Single().Editions.Should().NotBeNull();
            result.Single().AuthorMetadata.Value.ForeignAuthorId.Should().Be(metadataId);
            result.Single().Author.Value.Metadata.Value.Name.Should().Be("Terry Pratchett");
            _rreadingGlassesProvider.Verify(x => x.SearchForNewBook("guards", "pratchett", true), Times.Once);
            _rreadingGlassesProvider.Verify(x => x.GetBookInfo("book-1"), Times.Once);
            _openLibraryProvider.Verify(x => x.SearchForNewBook(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        }

        [Test]
        public void should_route_book_search_to_selected_providers()
        {
            var openLibraryMetadataId = MetadataIdentifier.Create("openlibrary", MetadataEntityType.Author, "1").ToString();
            var rreadingMetadataId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "2").ToString();

            var openLibrarySearchResults = new List<Book>
            {
                new Book
                {
                    ForeignBookId = "book-1",
                    AuthorMetadata = new AuthorMetadata { ForeignAuthorId = openLibraryMetadataId, Name = "Open Library Author" },
                    Editions = new List<Edition>
                    {
                        new Edition { ForeignEditionId = "edition-1", Isbn13 = "9781111111111", Monitored = true }
                    }
                }
            };

            var rreadingSearchResults = new List<Book>
            {
                new Book
                {
                    ForeignBookId = "book-2",
                    AuthorMetadata = new AuthorMetadata { ForeignAuthorId = rreadingMetadataId, Name = "Rreading Glasses Author" },
                    Editions = new List<Edition>
                    {
                        new Edition { ForeignEditionId = "edition-2", Isbn13 = "9782222222222", Monitored = true },
                        new Edition { ForeignEditionId = "edition-3", Isbn13 = "9782222222222", Monitored = false },
                        new Edition { ForeignEditionId = "edition-4", Isbn13 = "9783333333333", Monitored = true }
                    }
                }
            };

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataProvider)
                .Returns("openlibrary");

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataSearchProviders)
                .Returns("openlibrary, rreading-glasses");

            _openLibraryProvider
                .Setup(x => x.SearchForNewBook("guardians", "pratchett", true))
                .Returns(openLibrarySearchResults);

            _rreadingGlassesProvider
                .Setup(x => x.SearchForNewBook("guardians", "pratchett", true))
                .Returns(rreadingSearchResults);

            var result = Subject.SearchForNewBook("guardians", "pratchett", true);

            result.Should().HaveCount(2);
            result.Sum(x => x.Editions.Value.Count).Should().Be(3);
            _openLibraryProvider.Verify(x => x.SearchForNewBook("guardians", "pratchett", true), Times.Once);
            _rreadingGlassesProvider.Verify(x => x.SearchForNewBook("guardians", "pratchett", true), Times.Once);
            _openLibraryProvider.Verify(x => x.GetBookInfo(It.IsAny<string>()), Times.Never);
            _rreadingGlassesProvider.Verify(x => x.GetBookInfo(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void should_route_author_search_to_selected_providers()
        {
            var openLibraryAuthor = new Author { Metadata = new AuthorMetadata { ForeignAuthorId = "openlibrary:author:OL100", Name = "Neil Gaiman" } };
            var rreadingAuthor = new Author { Metadata = new AuthorMetadata { ForeignAuthorId = "rreading-glasses:author:RG100", Name = "Neil Gaiman" } };

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataSearchProviders)
                .Returns("rreading-glasses,openlibrary");

            _openLibraryProvider
                .Setup(x => x.SearchForNewAuthor("neil"))
                .Returns(new List<Author> { openLibraryAuthor });

            _rreadingGlassesProvider
                .Setup(x => x.SearchForNewAuthor("neil"))
                .Returns(new List<Author> { rreadingAuthor });

            var result = Subject.SearchForNewAuthor("neil");

            result.Should().HaveCount(2);
            _openLibraryProvider.Verify(x => x.SearchForNewAuthor("neil"), Times.Once);
            _rreadingGlassesProvider.Verify(x => x.SearchForNewAuthor("neil"), Times.Once);
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

        [Test]
        public void should_fall_back_to_independent_provider_for_isbn_lookup()
        {
            var isbn = "9780439554930";
            var metadata = new AuthorMetadata
            {
                ForeignAuthorId = "openlibrary:author:OL1A",
                Name = "Example Author"
            };
            var fallbackBook = new Book
            {
                ForeignBookId = "openlibrary:work:OL1W",
                AuthorMetadata = metadata,
                Author = new Author { Metadata = metadata },
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        ForeignEditionId = "openlibrary:edition:OL1M",
                        Isbn13 = isbn,
                        Monitored = true
                    }
                }
            };

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.MetadataProvider)
                .Returns("rreading-glasses");

            _rreadingGlassesProvider
                .Setup(x => x.SearchByIsbn(isbn))
                .Throws(new Exception("provider unavailable"));

            _openLibraryProvider
                .Setup(x => x.SearchByIsbn(isbn))
                .Returns(new List<Book> { fallbackBook });

            var result = Subject.SearchByIsbn(isbn);

            result.Should().ContainSingle();
            result[0].ForeignBookId.Should().Be("openlibrary:work:OL1W");
            _rreadingGlassesProvider.Verify(x => x.SearchByIsbn(isbn), Times.Once);
            _openLibraryProvider.Verify(x => x.SearchByIsbn(isbn), Times.Once);
            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_include_magazine_results_in_combined_search()
        {
            Mocker.GetMock<IMagazineTitleAuthorityProvider>()
                .Setup(x => x.LookupByTitleAsync("Wired Magazine", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MagazineAuthorityResult
                {
                    CanonicalTitle = "Wired Magazine",
                    NormalizedTitle = "wired magazine",
                    WikidataId = "Q12345",
                    Publisher = "American technology magazine."
                });

            var result = Subject.SearchForNewEntity("Wired Magazine");

            result.OfType<Magazine>().Should().ContainSingle(m => m.WikidataId == "Q12345");
            result.OfType<Magazine>().Single().Title.Should().Be("Wired Magazine");
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
