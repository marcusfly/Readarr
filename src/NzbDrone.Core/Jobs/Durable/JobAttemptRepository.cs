using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Jobs.Durable
{
    public class JobAttemptRepository : BasicRepository<JobAttempt>, IJobAttemptRepository
    {
        public JobAttemptRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public JobAttempt FindByIdempotencyKey(string key)
        {
            return Query(x => x.IdempotencyKey == key).FirstOrDefault();
        }

        public JobAttempt FindByCommandId(int commandId)
        {
            return Query(x => x.CommandId == commandId).FirstOrDefault();
        }

        public List<JobAttempt> GetByState(JobState state)
        {
            return Query(x => x.State == state);
        }

        public List<JobAttempt> GetStuckRunning()
        {
            return Query(x => x.State == JobState.Running);
        }
    }
}
