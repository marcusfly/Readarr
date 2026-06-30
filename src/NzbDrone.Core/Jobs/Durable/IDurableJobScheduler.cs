using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Jobs.Durable
{
    public interface IDurableJobScheduler
    {
        JobAttempt Submit<TCommand>(TCommand command,
                                    string idempotencyKey,
                                    CommandPriority priority = CommandPriority.Normal,
                                    CommandTrigger trigger = CommandTrigger.Unspecified)
            where TCommand : Command;
    }
}
