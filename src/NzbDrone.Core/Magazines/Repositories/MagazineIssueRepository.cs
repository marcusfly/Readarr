using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineIssueRepository : IBasicRepository<MagazineIssue>
    {
        List<MagazineIssue> GetByMagazine(int magazineId);
        MagazineIssue FindByIdentity(int magazineId, int year, int month, int? day);
        List<MagazineIssue> GetMonitored(int magazineId);
        List<MagazineIssue> GetMissingFiles(int magazineId);
    }

    public class MagazineIssueRepository : BasicRepository<MagazineIssue>, IMagazineIssueRepository
    {
        public MagazineIssueRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<MagazineIssue> GetByMagazine(int magazineId)
        {
            return Query(s => s.MagazineId == magazineId).ToList();
        }

        public MagazineIssue FindByIdentity(int magazineId, int year, int month, int? day)
        {
            return Query(s => s.MagazineId == magazineId &&
                              s.IssueYear == year &&
                              s.IssueMonth == month &&
                              ((s.IssueDay == day) || (s.IssueDay == null && day == null)))
                .SingleOrDefault();
        }

        public List<MagazineIssue> GetMonitored(int magazineId)
        {
            return Query(s => s.MagazineId == magazineId && s.Monitored == true).ToList();
        }

        public List<MagazineIssue> GetMissingFiles(int magazineId)
        {
#pragma warning disable CS0472
            return QueryDistinct(Builder()
                                .LeftJoin<MagazineIssue, MagazineIssueFile>((issue, file) => issue.Id == file.MagazineIssueId)
                                .Where<MagazineIssue>(s => s.MagazineId == magazineId && s.Monitored)
                                .Where<MagazineIssueFile>(s => s.Id == null));
#pragma warning restore CS0472
        }
    }
}
