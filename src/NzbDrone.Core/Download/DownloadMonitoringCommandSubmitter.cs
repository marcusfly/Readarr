using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Download
{
    public class DownloadMonitoringCommandSubmitter : IDownloadMonitoringCommandSubmitter
    {
        private readonly IDurableJobScheduler _durableJobScheduler;

        public DownloadMonitoringCommandSubmitter(IDurableJobScheduler durableJobScheduler)
        {
            _durableJobScheduler = durableJobScheduler;
        }

        public JobAttempt Submit(RefreshMonitoredDownloadsCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, "refresh-monitored-downloads", priority, trigger);
        }

        public JobAttempt Submit(ProcessMonitoredDownloadsCommand command,
                                 CommandPriority priority = CommandPriority.Normal,
                                 CommandTrigger trigger = CommandTrigger.Unspecified)
        {
            return _durableJobScheduler.Submit(command, "process-monitored-downloads", priority, trigger);
        }
    }
}
