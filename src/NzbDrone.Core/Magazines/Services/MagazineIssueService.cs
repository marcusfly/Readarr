using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineIssueService
    {
        MagazineIssue GetIssue(int id);
        List<MagazineIssue> GetIssuesByMagazine(int magazineId);
        MagazineIssue UpsertIssue(MagazineIssue issue);
        void MarkIssueMonitored(int issueId, bool monitored);
        List<MagazineIssue> GetMissingIssues(int magazineId);
    }

    public class MagazineIssueService : IMagazineIssueService
    {
        private readonly IMagazineIssueRepository _magazineIssueRepository;

        public MagazineIssueService(IMagazineIssueRepository magazineIssueRepository)
        {
            _magazineIssueRepository = magazineIssueRepository;
        }

        public MagazineIssue GetIssue(int id)
        {
            return _magazineIssueRepository.Get(id);
        }

        public List<MagazineIssue> GetIssuesByMagazine(int magazineId)
        {
            return _magazineIssueRepository.GetByMagazine(magazineId);
        }

        public MagazineIssue UpsertIssue(MagazineIssue issue)
        {
            if (issue == null)
            {
                throw new ArgumentNullException(nameof(issue));
            }

            var existing = _magazineIssueRepository.FindByIdentity(issue.MagazineId, issue.IssueYear, issue.IssueMonth, issue.IssueDay);
            if (existing == null)
            {
                if (issue.Added == default)
                {
                    issue.Added = DateTime.UtcNow;
                }

                _magazineIssueRepository.Insert(issue);
                return issue;
            }

            existing.IssueNumber = issue.IssueNumber;
            existing.Volume = issue.Volume;
            existing.ReleaseTitle = issue.ReleaseTitle;
            existing.Monitored = issue.Monitored;
            existing.LastSearchTime = issue.LastSearchTime;

            _magazineIssueRepository.Update(existing);
            return existing;
        }

        public void MarkIssueMonitored(int issueId, bool monitored)
        {
            var issue = _magazineIssueRepository.Get(issueId);

            issue.Monitored = monitored;
            _magazineIssueRepository.Update(issue);
        }

        public List<MagazineIssue> GetMissingIssues(int magazineId)
        {
            return _magazineIssueRepository.GetMissingFiles(magazineId).ToList();
        }
    }
}
