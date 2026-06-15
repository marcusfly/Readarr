using System.Collections.Generic;
using System.Linq;
using MonitorTypes = NzbDrone.Core.Books.MonitorTypes;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineMonitoredService
    {
        void SetIssueMonitoredStatus(Magazine magazine, MonitorTypes monitorType);
    }

    public class MagazineMonitoredService : IMagazineMonitoredService
    {
        private readonly IMagazineIssueService _magazineIssueService;

        public MagazineMonitoredService(IMagazineIssueService magazineIssueService)
        {
            _magazineIssueService = magazineIssueService;
        }

        public void SetIssueMonitoredStatus(Magazine magazine, MonitorTypes monitorType)
        {
            var issues = _magazineIssueService.GetIssuesByMagazine(magazine.Id);
            if (!issues.Any())
            {
                return;
            }

            var orderedIssues = issues
                .OrderBy(i => i.IssueYear)
                .ThenBy(i => i.IssueMonth)
                .ThenBy(i => i.IssueDay ?? 0)
                .ToList();

            switch (monitorType)
            {
                case MonitorTypes.All:
                    SetMonitored(issues, true);
                    break;
                case MonitorTypes.None:
                    SetMonitored(issues, false);
                    break;
                case MonitorTypes.First:
                    SetMonitored(issues, false);
                    _magazineIssueService.MarkIssueMonitored(orderedIssues.First().Id, true);
                    break;
                case MonitorTypes.Latest:
                    SetMonitored(issues, false);
                    _magazineIssueService.MarkIssueMonitored(orderedIssues.Last().Id, true);
                    break;
                case MonitorTypes.Missing:
                    SetMonitored(issues, false);
                    foreach (var issue in issues.Where(i => !HasFiles(i)))
                    {
                        _magazineIssueService.MarkIssueMonitored(issue.Id, true);
                    }

                    break;
                case MonitorTypes.Existing:
                    SetMonitored(issues, false);
                    foreach (var issue in issues.Where(HasFiles))
                    {
                        _magazineIssueService.MarkIssueMonitored(issue.Id, true);
                    }

                    break;
                case MonitorTypes.Future:
                default:
                    break;
            }
        }

        private void SetMonitored(IEnumerable<MagazineIssue> issues, bool monitored)
        {
            foreach (var issue in issues)
            {
                _magazineIssueService.MarkIssueMonitored(issue.Id, monitored);
            }
        }

        private bool HasFiles(MagazineIssue issue)
        {
            return issue.IssueFiles?.Value != null && issue.IssueFiles.Value.Any();
        }
    }
}
