using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Jobs.Durable
{
    public class DurableJobUpdatedEvent : IEvent
    {
        public JobAttempt JobAttempt { get; set; }

        public DurableJobUpdatedEvent(JobAttempt jobAttempt)
        {
            JobAttempt = jobAttempt;
        }
    }
}
