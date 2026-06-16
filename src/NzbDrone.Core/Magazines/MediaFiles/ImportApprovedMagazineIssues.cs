using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines.MediaFiles
{
    public interface IImportApprovedMagazineIssues
    {
        void Import(List<MagazineImportDecision> decisions, bool newDownload);
    }

    public class ImportApprovedMagazineIssues : IImportApprovedMagazineIssues
    {
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineIssueFileService _magazineIssueFileService;
        private readonly IEventAggregator _eventAggregator;

        public ImportApprovedMagazineIssues(IMagazineIssueService magazineIssueService,
                                            IMagazineIssueFileService magazineIssueFileService,
                                            IEventAggregator eventAggregator)
        {
            _magazineIssueService = magazineIssueService;
            _magazineIssueFileService = magazineIssueFileService;
            _eventAggregator = eventAggregator;
        }

        public void Import(List<MagazineImportDecision> decisions, bool newDownload)
        {
            if (decisions == null)
            {
                return;
            }

            foreach (var decision in decisions.Where(x => x?.Approved == true))
            {
                var localIssue = decision.LocalIssue;
                var parsed = localIssue.ParsedInfo;

                var issue = localIssue.Issue ?? new MagazineIssue
                {
                    MagazineId = localIssue.Magazine.Id,
                    IssueYear = parsed.IssueYear,
                    IssueMonth = parsed.IssueMonth,
                    IssueDay = parsed.IssueDay,
                    Volume = parsed.Volume,
                    IssueNumber = parsed.IssueNumber,
                    ReleaseTitle = parsed.ReleaseTitle,
                    Monitored = true,
                    Added = DateTime.UtcNow
                };

                issue = _magazineIssueService.UpsertIssue(issue);

                var existing = _magazineIssueFileService.GetFilesForIssue(issue.Id)
                    .FirstOrDefault(x => PathEqualityComparer.Instance.Equals(x.Path, localIssue.Path.FullName));

                if (existing == null)
                {
                    existing = new MagazineIssueFile
                    {
                        MagazineIssueId = issue.Id,
                        MagazineId = localIssue.Magazine.Id,
                        Path = localIssue.Path.FullName,
                        Size = localIssue.Path.Length,
                        DateAdded = DateTime.UtcNow,
                        Quality = localIssue.Quality
                    };

                    _magazineIssueFileService.Add(existing);
                }
                else
                {
                    existing.Size = localIssue.Path.Length;
                    existing.Quality = localIssue.Quality;
                    _magazineIssueFileService.Update(existing);
                }

                _eventAggregator.PublishEvent(new MagazineIssueFileImportedEvent(issue, existing));
            }
        }
    }
}
