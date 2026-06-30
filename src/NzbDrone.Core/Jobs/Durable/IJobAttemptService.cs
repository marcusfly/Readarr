using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Jobs.Durable
{
    public interface IJobAttemptService
    {
        JobAttempt GetById(int id);
        JobAttempt FindByCommandId(int commandId);
        List<JobAttempt> GetAll();
        List<JobAttempt> GetByState(JobState state);
        List<JobAttempt> GetPendingReplay();
        JobAttempt Submit(Command command, string jobType, string idempotencyKey, CommandPriority priority, CommandTrigger trigger);
        void MarkRunning(JobAttempt attempt, System.Guid leaseToken, int commandId);
        void MarkCompleted(JobAttempt attempt);
        void MarkFailed(JobAttempt attempt, string error, int maxAttempts);
        void MarkCanceled(JobAttempt attempt);
        void UpdateProgress(JobAttempt attempt, int progress);
        List<JobAttempt> GetStuckRunning();
        void RequeueStuck(JobAttempt attempt);
    }
}
