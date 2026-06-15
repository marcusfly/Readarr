using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineIssueFileRepository : IBasicRepository<MagazineIssueFile>
    {
        List<MagazineIssueFile> GetFilesByMagazine(int magazineId);
        List<MagazineIssueFile> GetFilesByIssue(int issueId);
    }

    public class MagazineIssueFileRepository : BasicRepository<MagazineIssueFile>, IMagazineIssueFileRepository
    {
        public MagazineIssueFileRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<MagazineIssueFile> GetFilesByMagazine(int magazineId)
        {
            return Query(s => s.MagazineId == magazineId).ToList();
        }

        public List<MagazineIssueFile> GetFilesByIssue(int issueId)
        {
            return Query(s => s.MagazineIssueId == issueId).ToList();
        }
    }
}
