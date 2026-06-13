using System;
using NzbDrone.Core.Jobs.Durable;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Jobs
{
    public class JobAttemptResource : RestResource
    {
        public string JobType { get; set; }
        public string IdempotencyKey { get; set; }
        public JobState State { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int AttemptCount { get; set; }
        public string LastError { get; set; }
        public int Progress { get; set; }
    }

    public static class JobAttemptResourceMapper
    {
        public static JobAttemptResource ToResource(this JobAttempt model)
        {
            if (model == null)
            {
                return null;
            }

            return new JobAttemptResource
            {
                Id = model.Id,
                JobType = model.JobType,
                IdempotencyKey = model.IdempotencyKey,
                State = model.State,
                QueuedAt = model.QueuedAt,
                StartedAt = model.StartedAt,
                CompletedAt = model.CompletedAt,
                AttemptCount = model.AttemptCount,
                LastError = model.LastError,
                Progress = model.Progress
            };
        }
    }
}
