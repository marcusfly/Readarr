using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Download
{
    public interface IDownloadMonitoringCommandSubmitter
    {
        JobAttempt Submit(RefreshMonitoredDownloadsCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);

        JobAttempt Submit(ProcessMonitoredDownloadsCommand command,
                          CommandPriority priority = CommandPriority.Normal,
                          CommandTrigger trigger = CommandTrigger.Unspecified);
    }
}
