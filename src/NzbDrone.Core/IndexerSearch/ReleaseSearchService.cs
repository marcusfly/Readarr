using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.IndexerSearch
{
    public interface ISearchForReleases
    {
        Task<List<DownloadDecision>> BookSearch(int bookId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> AuthorSearch(int authorId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> MagazineIssueSearch(int magazineIssueId, bool userInvokedSearch, bool interactiveSearch);
    }

    public class ReleaseSearchService : ISearchForReleases
    {
        private readonly IIndexerFactory _indexerFactory;
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMakeDownloadDecision _makeDownloadDecision;
        private readonly Logger _logger;

        public ReleaseSearchService(IIndexerFactory indexerFactory,
                                IBookService bookService,
                                IAuthorService authorService,
                                IMagazineIssueService magazineIssueService,
                                IMakeDownloadDecision makeDownloadDecision,
                                Logger logger)
        {
            _indexerFactory = indexerFactory;
            _bookService = bookService;
            _authorService = authorService;
            _magazineIssueService = magazineIssueService;
            _makeDownloadDecision = makeDownloadDecision;
            _logger = logger;
        }

        public async Task<List<DownloadDecision>> BookSearch(int bookId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var book = _bookService.GetBook(bookId);

            var decisions = await BookSearch(book, missingOnly, userInvokedSearch, interactiveSearch);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        public async Task<List<DownloadDecision>> AuthorSearch(int authorId, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var author = _authorService.GetAuthor(authorId);

            var decisions = await AuthorSearch(author, missingOnly, userInvokedSearch, interactiveSearch);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        public async Task<List<DownloadDecision>> AuthorSearch(Author author, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var searchSpec = Get<AuthorSearchCriteria>(author, userInvokedSearch, interactiveSearch);
            var books = _bookService.GetBooksByAuthor(author.Id);

            books = books.Where(a => a.Monitored).ToList();

            searchSpec.Books = books;

            return await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        public async Task<List<DownloadDecision>> MagazineIssueSearch(int magazineIssueId, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var issue = _magazineIssueService.GetIssue(magazineIssueId);
            if (issue == null)
            {
                _logger.Warn("Unable to find magazine issue {0} for search.", magazineIssueId);
                return downloadDecisions;
            }

            var decisions = await MagazineIssueSearch(issue, userInvokedSearch, interactiveSearch);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        public async Task<List<DownloadDecision>> MagazineIssueSearch(MagazineIssue issue, bool userInvokedSearch, bool interactiveSearch)
        {
            var searchSpec = Get(issue, userInvokedSearch, interactiveSearch);

            if (searchSpec.MagazineTitle.IsNullOrWhiteSpace())
            {
                _logger.Warn("Unable to search magazine issue {0} because no magazine title was available.", issue.Id);
                return new List<DownloadDecision>();
            }

            return await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        public async Task<List<DownloadDecision>> BookSearch(Book book, bool missingOnly, bool userInvokedSearch, bool interactiveSearch)
        {
            var author = _authorService.GetAuthor(book.AuthorId);

            var searchSpec = Get<BookSearchCriteria>(author, new List<Book> { book }, userInvokedSearch, interactiveSearch);

            searchSpec.BookTitle = book.GetBestMonitoredEdition()?.Title ?? book.Title;

            // searchSpec.BookIsbn = book.Isbn13;
            if (book.ReleaseDate.HasValue)
            {
                searchSpec.BookYear = book.ReleaseDate.Value.Year;
            }

            return await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
        }

        private TSpec Get<TSpec>(Author author, List<Book> books, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();

            spec.Books = books;
            spec.Author = author;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            return spec;
        }

        private static TSpec Get<TSpec>(Author author, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec();
            spec.Author = author;
            spec.UserInvokedSearch = userInvokedSearch;
            spec.InteractiveSearch = interactiveSearch;

            return spec;
        }

        private static MagazineIssueSearchCriteria Get(MagazineIssue issue, bool userInvokedSearch, bool interactiveSearch)
        {
            var magazine = issue.Magazine?.Value;

            return new MagazineIssueSearchCriteria
            {
                Magazine = magazine,
                Issue = issue,
                MagazineTitle = magazine?.Title ?? issue.ReleaseTitle,
                IssueYear = issue.IssueYear,
                IssueMonth = issue.IssueMonth,
                IssueDay = issue.IssueDay,
                UserInvokedSearch = userInvokedSearch,
                InteractiveSearch = interactiveSearch
            };
        }

        private async Task<List<DownloadDecision>> Dispatch(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, SearchCriteriaBase criteriaBase)
        {
            var indexers = criteriaBase.InteractiveSearch ?
                _indexerFactory.InteractiveSearchEnabled() :
                _indexerFactory.AutomaticSearchEnabled();

            // Filter indexers to untagged indexers and indexers with intersecting tags
            IEnumerable<int> criteriaTags = criteriaBase.Tags != null ? criteriaBase.Tags : Array.Empty<int>();
            indexers = indexers.Where(i => i.Definition.Tags.Empty() || (criteriaTags.Any() && i.Definition.Tags.Intersect(criteriaTags).Any())).ToList();

            _logger.ProgressInfo("Searching indexers for {0}. {1} active indexers", criteriaBase, indexers.Count);

            var tasks = indexers.Select(indexer => DispatchIndexer(searchAction, indexer, criteriaBase));

            var batch = await Task.WhenAll(tasks);

            var reports = batch.SelectMany(x => x).ToList();

            _logger.ProgressDebug("Total of {0} reports were found for {1} from {2} indexers", reports.Count, criteriaBase, indexers.Count);

            // Update the last search time for the searched items if at least 1 indexer was searched.
            if (indexers.Any())
            {
                var lastSearchTime = DateTime.UtcNow;
                _logger.Debug("Setting last search time to: {0}", lastSearchTime);

                if (criteriaBase is MagazineIssueSearchCriteria magazineIssueSearchCriteria)
                {
                    magazineIssueSearchCriteria.Issue.LastSearchTime = lastSearchTime;
                    _magazineIssueService.UpsertIssue(magazineIssueSearchCriteria.Issue);
                }
                else
                {
                    criteriaBase.Books.ForEach(a => a.LastSearchTime = lastSearchTime);
                    _bookService.UpdateLastSearchTime(criteriaBase.Books);
                }
            }

            return _makeDownloadDecision.GetSearchDecision(reports, criteriaBase).ToList();
        }

        private async Task<IList<ReleaseInfo>> DispatchIndexer(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, IIndexer indexer, SearchCriteriaBase criteriaBase)
        {
            try
            {
                return await searchAction(indexer);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while searching for {0}", criteriaBase);
            }

            return Array.Empty<ReleaseInfo>();
        }

        private List<DownloadDecision> DeDupeDecisions(List<DownloadDecision> decisions)
        {
            // De-dupe reports by guid so duplicate results aren't returned. Pick the one with the least rejections and higher indexer priority.
            return decisions.GroupBy(d => d.RemoteBook.Release.Guid)
                .Select(d => d.OrderBy(v => v.Rejections.Count()).ThenBy(v => v.RemoteBook?.Release?.IndexerPriority ?? IndexerDefinition.DefaultPriority).First())
                .ToList();
        }
    }
}
