using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Jobs.Durable
{
    public class JobAttemptService : IJobAttemptService
    {
        private readonly IJobAttemptRepository _repo;
        private readonly Logger _logger;

        public JobAttemptService(IJobAttemptRepository repo, Logger logger)
        {
            _repo = repo;
            _logger = logger;
        }

        public JobAttempt FindByCommandId(int commandId)
        {
            return _repo.FindByCommandId(commandId);
        }

        public JobAttempt GetById(int id)
        {
            return _repo.Get(id);
        }

        public List<JobAttempt> GetAll()
        {
            return new List<JobAttempt>(_repo.All());
        }

        public List<JobAttempt> GetByState(JobState state)
        {
            return _repo.GetByState(state);
        }

        public List<JobAttempt> GetPendingReplay()
        {
            return _repo.GetPendingReplay();
        }

        public JobAttempt Submit(Command command, string jobType, string idempotencyKey, CommandPriority priority, CommandTrigger trigger)
        {
            var existing = _repo.FindByIdempotencyKey(idempotencyKey);

            if (existing != null &&
                (existing.State == JobState.Queued || existing.State == JobState.Running))
            {
                _logger.Trace("Job with idempotency key {0} is already {1}; skipping submission", idempotencyKey, existing.State);
                return existing;
            }

            var attempt = new JobAttempt
            {
                CommandBody = command,
                CommandPriority = priority,
                CommandTrigger = trigger,
                JobType = jobType,
                IdempotencyKey = idempotencyKey,
                State = JobState.Queued,
                QueuedAt = DateTime.UtcNow,
                AttemptCount = 0,
                Progress = 0
            };

            _repo.Insert(attempt);
            _logger.Debug("Queued durable job {0} with key {1} (id={2})", jobType, idempotencyKey, attempt.Id);
            return attempt;
        }

        public void MarkRunning(JobAttempt attempt, Guid leaseToken, int commandId)
        {
            attempt.State = JobState.Running;
            attempt.StartedAt = DateTime.UtcNow;
            attempt.CompletedAt = null;
            attempt.LeaseToken = leaseToken;
            attempt.CommandId = commandId;
            attempt.AttemptCount++;
            attempt.LastError = null;
            attempt.Progress = 0;
            _repo.SetFields(attempt,
                a => a.State,
                a => a.StartedAt,
                a => a.CompletedAt,
                a => a.LeaseToken,
                a => a.CommandId,
                a => a.AttemptCount,
                a => a.LastError,
                a => a.Progress);
        }

        public void MarkCompleted(JobAttempt attempt)
        {
            attempt.State = JobState.Completed;
            attempt.CompletedAt = DateTime.UtcNow;
            attempt.Progress = 100;
            _repo.SetFields(attempt,
                a => a.State,
                a => a.CompletedAt,
                a => a.Progress);
        }

        public void MarkFailed(JobAttempt attempt, string error, int maxAttempts)
        {
            attempt.LastError = error;
            attempt.State = attempt.AttemptCount < maxAttempts ? JobState.Retrying : JobState.Failed;
            attempt.CompletedAt = DateTime.UtcNow;
            _repo.SetFields(attempt,
                a => a.State,
                a => a.LastError,
                a => a.CompletedAt);
        }

        public void MarkCanceled(JobAttempt attempt)
        {
            attempt.State = JobState.Canceled;
            attempt.CompletedAt = DateTime.UtcNow;
            _repo.SetFields(attempt,
                a => a.State,
                a => a.CompletedAt);
        }

        public void UpdateProgress(JobAttempt attempt, int progress)
        {
            attempt.Progress = progress;
            _repo.SetFields(attempt, a => a.Progress);
        }

        public List<JobAttempt> GetStuckRunning()
        {
            return _repo.GetStuckRunning();
        }

        public void RequeueStuck(JobAttempt attempt)
        {
            _logger.Info("Requeueing stuck job {0} (id={1})", attempt.JobType, attempt.Id);
            attempt.State = JobState.Retrying;
            attempt.LeaseToken = null;
            _repo.SetFields(attempt, a => a.State, a => a.LeaseToken);
        }
    }
}
