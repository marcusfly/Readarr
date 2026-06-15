using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Services
{
    public class MagazineIssueSearchService : IExecute<MagazineIssueSearchCommand>,
                                               IExecute<MissingMagazineIssueSearchCommand>
    {
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public MagazineIssueSearchService(IMagazineIssueService magazineIssueService,
                                          ISearchForReleases releaseSearchService,
                                          IProcessDownloadDecisions processDownloadDecisions,
                                          Logger logger)
        {
            _magazineIssueService = magazineIssueService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(MagazineIssueSearchCommand message)
        {
            if (message == null)
            {
                return;
            }

            var issueIds = message.MagazineIssueIds ?? new List<int>();
            if (issueIds.Count == 0)
            {
                _logger.Warn("Magazine issue search requested with no issue ids.");
                return;
            }

            var issues = issueIds.Select(id => _magazineIssueService.GetIssue(id))
                .Where(issue => issue != null)
                .ToList();

            if (issues.Count == 0)
            {
                _logger.Warn("Magazine issue search requested for unknown issue ids.");
                return;
            }

            SearchIssues(issues, "Magazine issue search");
        }

        public void Execute(MissingMagazineIssueSearchCommand message)
        {
            if (message == null)
            {
                return;
            }

            if (!message.MagazineId.HasValue)
            {
                _logger.Warn("Missing magazine issue search requested with no magazine id.");
                return;
            }

            var missing = _magazineIssueService.GetMissingIssues(message.MagazineId.Value);
            if (missing.Count == 0)
            {
                _logger.Info("No missing magazine issues found for magazine id {0}.", message.MagazineId.Value);
                return;
            }

            SearchIssues(missing, "Missing magazine issue search");
        }

        private void SearchIssues(List<MagazineIssue> issues, string description)
        {
            var grabbedCount = 0;

            _logger.ProgressInfo("Searching {0} magazine issue(s)", issues.Count);

            foreach (var issue in issues.OrderBy(v => v.LastSearchTime ?? DateTime.MinValue))
            {
                try
                {
                    var decisions = _releaseSearchService.MagazineIssueSearch(issue.Id, false, false).GetAwaiter().GetResult();
                    var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

                    grabbedCount += processed.Grabbed.Count;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for magazine issue: [{0}]", issue);
                }
            }

            _logger.ProgressInfo("{0} completed. {1} reports downloaded.", description, grabbedCount);
        }
    }
}
