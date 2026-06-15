using System;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Services
{
    public class RescanMagazineService : IExecute<RescanMagazineCommand>
    {
        private readonly IMagazineService _magazineService;
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public RescanMagazineService(IMagazineService magazineService,
                                     IMagazineIssueService magazineIssueService,
                                     ISearchForReleases releaseSearchService,
                                     IProcessDownloadDecisions processDownloadDecisions,
                                     Logger logger)
        {
            _magazineService = magazineService;
            _magazineIssueService = magazineIssueService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(RescanMagazineCommand message)
        {
            if (message == null)
            {
                return;
            }

            var magazineIds = message.MagazineIds;
            if (magazineIds == null || magazineIds.Count == 0)
            {
                _logger.Warn("Magazine rescan requested with no magazine ids.");
                return;
            }

            _logger.Info("Rescanning {0} magazines. AddNewIssues={1}", magazineIds.Count, message.AddNewIssues);
            foreach (var magazineId in magazineIds)
            {
                var magazine = _magazineService.GetMagazine(magazineId);
                if (magazine == null)
                {
                    _logger.Warn("Magazine with id {0} not found for rescan.", magazineId);
                    continue;
                }

                var issues = _magazineIssueService.GetMissingIssues(magazineId);
                if (issues.Count == 0)
                {
                    _logger.Info("No missing magazine issues found for '{0}'.", magazine.Title);
                    continue;
                }

                _logger.ProgressInfo("Searching for {0} missing issues for '{1}'", issues.Count, magazine.Title);

                foreach (var issue in issues)
                {
                    try
                    {
                        var decisions = _releaseSearchService.MagazineIssueSearch(issue.Id, false, false).GetAwaiter().GetResult();
                        var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

                        _logger.ProgressInfo("Magazine rescan completed for {0}. {1} reports downloaded.", issue, processed.Grabbed.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Unable to rescan magazine issue: [{0}]", issue);
                    }
                }
            }
        }
    }
}
