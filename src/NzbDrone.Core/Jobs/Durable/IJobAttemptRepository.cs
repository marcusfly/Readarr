using System.Collections.Generic;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Jobs.Durable
{
    public interface IJobAttemptRepository : IBasicRepository<JobAttempt>
    {
        JobAttempt FindByIdempotencyKey(string key);
        JobAttempt FindByCommandId(int commandId);
        List<JobAttempt> GetByState(JobState state);
        List<JobAttempt> GetPendingReplay();
        List<JobAttempt> GetStuckRunning();
    }
}
