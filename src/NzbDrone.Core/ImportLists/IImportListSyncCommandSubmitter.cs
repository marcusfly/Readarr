using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.ImportLists
{
    public interface IImportListSyncCommandSubmitter
    {
        JobAttempt Submit(ImportListSyncCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);
    }
}
