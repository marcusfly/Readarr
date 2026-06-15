using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineRootFolderRepository : IBasicRepository<MagazineRootFolder>
    {
    }

    public class MagazineRootFolderRepository : BasicRepository<MagazineRootFolder>, IMagazineRootFolderRepository
    {
        public MagazineRootFolderRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }
    }
}
