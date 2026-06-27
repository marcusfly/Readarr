using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Magazines.Services
{
    public class MagazineIssueSearchService : IExecute<MagazineIssueSearchCommand>,
                                               IExecute<MagazineSearchCommand>,
                                               IExecute<MissingMagazineIssueSearchCommand>
    {
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineService _magazineService;
        private readonly IMagazineMonitoredService _magazineMonitoredService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IDownloadService _downloadService;
        private readonly Logger _logger;

        public MagazineIssueSearchService(IMagazineIssueService magazineIssueService,
                                          IMagazineService magazineService,
                                          IMagazineMonitoredService magazineMonitoredService,
                                          ISearchForReleases releaseSearchService,
                                          IDownloadService downloadService,
                                          Logger logger)
        {
            _magazineIssueService = magazineIssueService;
            _magazineService = magazineService;
            _magazineMonitoredService = magazineMonitoredService;
            _releaseSearchService = releaseSearchService;
            _downloadService = downloadService;
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

        public void Execute(MagazineSearchCommand message)
        {
            if (message?.MagazineId == null)
            {
                _logger.Warn("Magazine search requested with no magazine id.");
                return;
            }

            _logger.ProgressInfo("Searching recent releases for magazine id {0}", message.MagazineId.Value);

            try
            {
                var decisions = _releaseSearchService.MagazineSearch(message.MagazineId.Value, true, false).GetAwaiter().GetResult();
                var discoveredCount = PersistDiscoveredIssues(message.MagazineId.Value, decisions);
                var grabbedCount = ProcessMagazineDecisions(decisions, $"Magazine search for magazine id {message.MagazineId.Value}");

                _logger.ProgressInfo("Magazine search completed. {0} issues discovered, {1} reports downloaded.", discoveredCount, grabbedCount);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to search for magazine id: [{0}]", message.MagazineId.Value);
            }
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
                    grabbedCount += ProcessMagazineDecisions(decisions, $"Magazine issue search for {issue.ReleaseTitle ?? issue.Id.ToString()}");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unable to search for magazine issue: [{0}]", issue);
                }
            }

            _logger.ProgressInfo("{0} completed. {1} reports downloaded.", description, grabbedCount);
        }

        private int PersistDiscoveredIssues(int magazineId, List<DownloadDecision> decisions)
        {
            if (decisions == null || decisions.Count == 0)
            {
                return 0;
            }

            var hadExistingIssues = _magazineIssueService.GetIssuesByMagazine(magazineId).Count > 0;

            var persistedIssues = decisions
                .Select(decision => decision.RemoteBook as RemoteMagazineIssue)
                .Where(remoteIssue => remoteIssue?.Issue != null && remoteIssue.Magazine?.Id == magazineId)
                .Select(remoteIssue => remoteIssue.Issue)
                .Where(issue => issue.IssueYear > 0 && issue.IssueMonth > 0)
                .GroupBy(issue => (issue.MagazineId, issue.IssueYear, issue.IssueMonth, issue.IssueDay))
                .Select(group => _magazineIssueService.UpsertIssue(group.First()))
                .ToList();

            if (persistedIssues.Count == 0)
            {
                return 0;
            }

            if (!hadExistingIssues)
            {
                var magazine = _magazineService.GetMagazine(magazineId);
                if (magazine != null)
                {
                    _magazineMonitoredService.SetIssueMonitoredStatus(magazine, magazine.AddOptions?.Monitor ?? MonitorTypes.All);
                }
            }

            var persistedByIdentity = persistedIssues.ToDictionary(
                issue => (issue.MagazineId, issue.IssueYear, issue.IssueMonth, issue.IssueDay));

            foreach (var remoteIssue in decisions.Select(decision => decision.RemoteBook as RemoteMagazineIssue).Where(remoteIssue => remoteIssue?.Issue != null))
            {
                if (persistedByIdentity.TryGetValue((remoteIssue.Issue.MagazineId, remoteIssue.Issue.IssueYear, remoteIssue.Issue.IssueMonth, remoteIssue.Issue.IssueDay), out var persisted))
                {
                    remoteIssue.Issue = persisted;
                    remoteIssue.DownloadAllowed = remoteIssue.Magazine != null;
                }
            }

            return persistedIssues.Count;
        }

        private int ProcessMagazineDecisions(List<DownloadDecision> decisions, string description)
        {
            if (decisions == null || decisions.Count == 0)
            {
                _logger.ProgressInfo("{0}: no candidate releases returned.", description);
                return 0;
            }

            var magazineDecisions = decisions
                .Where(decision => decision.RemoteBook is RemoteMagazineIssue)
                .ToList();

            var approved = magazineDecisions
                .Where(IsQualifiedMagazineDecision)
                .OrderByDescending(decision => GetIssueSortValue((RemoteMagazineIssue)decision.RemoteBook))
                .ToList();

            var temporarilyRejected = magazineDecisions.Where(decision => decision.TemporarilyRejected).ToList();
            var permanentlyRejected = magazineDecisions.Where(decision => decision.Rejected).ToList();
            var unqualifiedApproved = magazineDecisions.Where(decision => decision.Approved && !IsQualifiedMagazineDecision(decision)).ToList();

            LogDecisionSummary(description, magazineDecisions.Count, approved.Count, temporarilyRejected.Count, permanentlyRejected, unqualifiedApproved.Count);

            var grabbedCount = 0;
            var seenIssues = new HashSet<(int MagazineId, int Year, int Month, int? Day)>();

            foreach (var decision in approved)
            {
                var remoteIssue = (RemoteMagazineIssue)decision.RemoteBook;
                var identity = (remoteIssue.Issue.MagazineId, remoteIssue.Issue.IssueYear, remoteIssue.Issue.IssueMonth, remoteIssue.Issue.IssueDay);

                if (!seenIssues.Add(identity))
                {
                    continue;
                }

                try
                {
                    _downloadService.DownloadReport(remoteIssue, null).GetAwaiter().GetResult();
                    grabbedCount++;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Couldn't add magazine report to download queue. {0}", remoteIssue.Release?.Title ?? remoteIssue.ToString());
                }
            }

            return grabbedCount;
        }

        private static bool IsQualifiedMagazineDecision(DownloadDecision decision)
        {
            if (!decision.Approved || !decision.RemoteBook.DownloadAllowed)
            {
                return false;
            }

            return decision.RemoteBook is RemoteMagazineIssue remoteIssue &&
                   remoteIssue.Magazine != null &&
                   remoteIssue.Issue != null &&
                   remoteIssue.Release != null;
        }

        private void LogDecisionSummary(string description, int total, int approvedCount, int temporarilyRejectedCount, List<DownloadDecision> permanentlyRejected, int unqualifiedApprovedCount)
        {
            var rejectionSummary = string.Join("; ", permanentlyRejected
                .SelectMany(decision => decision.Rejections)
                .GroupBy(rejection => rejection.Reason)
                .OrderByDescending(group => group.Count())
                .Take(5)
                .Select(group => $"{group.Key} x{group.Count()}"));

            _logger.ProgressInfo(
                "{0}: {1} candidates, {2} approved, {3} temporarily rejected, {4} permanently rejected, {5} unqualified approvals.{6}",
                description,
                total,
                approvedCount,
                temporarilyRejectedCount,
                permanentlyRejected.Count,
                unqualifiedApprovedCount,
                rejectionSummary.Any() ? $" Top rejections: {rejectionSummary}" : string.Empty);
        }

        private static long GetIssueSortValue(RemoteMagazineIssue remoteIssue)
        {
            var issue = remoteIssue.Issue;
            return issue == null
                ? 0
                : DateTime.SpecifyKind(new DateTime(issue.IssueYear, Math.Max(issue.IssueMonth, 1), issue.IssueDay ?? 1), DateTimeKind.Utc).Ticks;
        }
    }
}
