using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.ImportLists
{
    public class ImportListSyncCommandSubmitter : IImportListSyncCommandSubmitter
    {
        private readonly IDurableJobScheduler _durableJobScheduler;

        public ImportListSyncCommandSubmitter(IDurableJobScheduler durableJobScheduler)
        {
            _durableJobScheduler = durableJobScheduler;
        }

        public JobAttempt Submit(ImportListSyncCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, GetImportListSyncKey(command), priority, trigger);
        }

        private static string GetImportListSyncKey(ImportListSyncCommand command)
        {
            return $"import-list-sync:{command.DefinitionId?.ToString() ?? "all"}";
        }
    }
}
