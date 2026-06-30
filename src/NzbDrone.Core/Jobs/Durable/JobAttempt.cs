using System;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Jobs.Durable
{
    public class JobAttempt : ModelBase
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
        public Guid? LeaseToken { get; set; }

        /// <summary>
        /// Id of the correlated CommandModel in the existing command pipeline.
        /// Null until the attempt has been dispatched.
        /// </summary>
        public int? CommandId { get; set; }
        public Command CommandBody { get; set; }
        public CommandPriority CommandPriority { get; set; }
        public CommandTrigger CommandTrigger { get; set; }
    }
}
