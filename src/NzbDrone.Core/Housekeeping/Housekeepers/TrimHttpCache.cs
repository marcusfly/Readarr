using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Http;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class TrimHttpCache : IHousekeepingTask
    {
        private readonly ICacheDatabase _database;
        private readonly ICachedHttpResponseRepository _repository;

        public TrimHttpCache(ICacheDatabase database, ICachedHttpResponseRepository repository)
        {
            _database = database;
            _repository = repository;
        }

        public void Clean()
        {
            _repository.DeleteOlderThan(DateTime.UtcNow.AddDays(-CachedHttpResponse.StaleRetentionDays));
            _database.Vacuum();
        }
    }
}
