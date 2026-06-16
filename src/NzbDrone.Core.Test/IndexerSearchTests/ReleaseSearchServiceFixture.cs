using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.IndexerSearchTests
{
        public class ReleaseSearchServiceFixture : CoreTest<ReleaseSearchService>
    {
        private Mock<IIndexer> _mockIndexer;
        private Author _author;
        private Book _firstBook;
        private Magazine _magazine;
        private MagazineIssue _magazineIssue;

        [SetUp]
        public void SetUp()
        {
            _mockIndexer = Mocker.GetMock<IIndexer>();
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition { Id = 1 });
            _mockIndexer.SetupGet(s => s.SupportsSearch).Returns(true);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.AutomaticSearchEnabled(true))
                  .Returns(new List<IIndexer> { _mockIndexer.Object });

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetSearchDecision(It.IsAny<List<Parser.Model.ReleaseInfo>>(), It.IsAny<SearchCriteriaBase>()))
                .Returns(new List<DownloadDecision>());

            Mocker.GetMock<IMakeDownloadDecision>()
                .Setup(s => s.GetMagazineSearchDecision(It.IsAny<List<Parser.Model.ReleaseInfo>>(), It.IsAny<MagazineIssueSearchCriteria>()))
                .Returns(new List<DownloadDecision>());

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .Build();

            _firstBook = Builder<Book>.CreateNew()
                .With(e => e.Author = _author)
                .Build();

            var edition = Builder<Edition>.CreateNew()
                .With(e => e.Book = _firstBook)
                .With(e => e.Monitored = true)
                .Build();

            _firstBook.Editions = new List<Edition> { edition };

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            _magazine = Builder<Magazine>.CreateNew()
                .With(v => v.Tags = new HashSet<int> { 3 })
                .Build();

            _magazineIssue = Builder<MagazineIssue>.CreateNew()
                .With(v => v.MagazineId = _magazine.Id)
                .With(v => v.Magazine = _magazine)
                .With(v => v.IssueYear = 2024)
                .With(v => v.IssueMonth = 6)
                .With(v => v.IssueDay = 15)
                .With(v => v.ReleaseTitle = "Sample Magazine 2024-06-15")
                .Build();

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(v => v.GetIssue(_magazineIssue.Id))
                .Returns(_magazineIssue);

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(v => v.UpsertIssue(It.IsAny<MagazineIssue>()))
                .Returns<MagazineIssue>(v => v);
        }

        private List<SearchCriteriaBase> WatchForSearchCriteria()
        {
            var result = new List<SearchCriteriaBase>();

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<BookSearchCriteria>()))
                .Callback<BookSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            _mockIndexer.Setup(v => v.Fetch(It.IsAny<MagazineIssueSearchCriteria>()))
                .Callback<MagazineIssueSearchCriteria>(s => result.Add(s))
                .Returns(Task.FromResult<IList<Parser.Model.ReleaseInfo>>(new List<Parser.Model.ReleaseInfo>()));

            return result;
        }

        [Test]
        public async Task Tags_IndexerTags_AuthorNoTags_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 3 }
            });

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task Tags_IndexerNoTags_AuthorTags_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1
            });

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3 })
                .Build();

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndAuthorTagsMatch_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 3, 4, 5 })
                .Build();

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndAuthorTagsMismatch_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 1, 2, 3 }
            });

            _author = Builder<Author>.CreateNew()
                .With(v => v.Monitored = true)
                .With(v => v.Tags = new HashSet<int> { 4, 5, 6 })
                .Build();

            Mocker.GetMock<IAuthorService>()
                .Setup(v => v.GetAuthor(_author.Id))
                .Returns(_author);

            var allCriteria = WatchForSearchCriteria();

            await Subject.BookSearch(_firstBook, false, true, false);

            var criteria = allCriteria.OfType<BookSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task Tags_IndexerAndMagazineTagsMatch_IndexerIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 3 }
            });

            var allCriteria = WatchForSearchCriteria();

            await Subject.MagazineIssueSearch(_magazineIssue.Id, true, false);

            var criteria = allCriteria.OfType<MagazineIssueSearchCriteria>().ToList();

            criteria.Count.Should().Be(1);
        }

        [Test]
        public async Task Tags_IndexerAndMagazineTagsMismatch_IndexerNotIncluded()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1,
                Tags = new HashSet<int> { 7 }
            });

            var allCriteria = WatchForSearchCriteria();

            await Subject.MagazineIssueSearch(_magazineIssue.Id, true, false);

            var criteria = allCriteria.OfType<MagazineIssueSearchCriteria>().ToList();

            criteria.Count.Should().Be(0);
        }

        [Test]
        public async Task MagazineIssueSearch_Should_Update_LastSearchTime()
        {
            _mockIndexer.SetupGet(s => s.Definition).Returns(new IndexerDefinition
            {
                Id = 1
            });

            var allCriteria = WatchForSearchCriteria();

            await Subject.MagazineIssueSearch(_magazineIssue.Id, true, false);

            var criteria = allCriteria.OfType<MagazineIssueSearchCriteria>().ToList();
            criteria.Count.Should().Be(1);
            _magazineIssue.LastSearchTime.Should().NotBeNull();
            Mocker.GetMock<IMagazineIssueService>().Verify(v => v.UpsertIssue(It.IsAny<MagazineIssue>()), Times.Once);
        }
    }
}
