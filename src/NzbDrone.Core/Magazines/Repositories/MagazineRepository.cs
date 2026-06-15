using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineRepository : IBasicRepository<Magazine>
    {
        Magazine GetByTitle(string cleanTitle);
        Magazine GetByNormalizedTitle(string normalizedTitle);
        List<Magazine> GetAllMonitored();
        Magazine FindByPath(string path);
    }

    public class MagazineRepository : BasicRepository<Magazine>, IMagazineRepository
    {
        public MagazineRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public Magazine GetByTitle(string cleanTitle)
        {
            if (cleanTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            var normalizedCleanTitle = cleanTitle.ToLowerInvariant();
            return Query(s => s.CleanTitle == normalizedCleanTitle).SingleOrDefault();
        }

        public Magazine GetByNormalizedTitle(string normalizedTitle)
        {
            if (normalizedTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            var normalized = normalizedTitle.ToLowerInvariant();
            return Query(s => s.NormalizedTitle == normalized).SingleOrDefault();
        }

        public List<Magazine> GetAllMonitored()
        {
            return Query(s => s.Monitored).ToList();
        }

        public Magazine FindByPath(string path)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return null;
            }

            return Query(s => s.Path == path).SingleOrDefault();
        }
    }
}
