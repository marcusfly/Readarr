using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Books.Commands
{
    public interface IRefreshCommandSubmitter
    {
        JobAttempt Submit(RefreshAuthorCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);

        JobAttempt Submit(BulkRefreshAuthorCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);

        JobAttempt Submit(RefreshBookCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);

        JobAttempt Submit(BulkRefreshBookCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);
    }
}
