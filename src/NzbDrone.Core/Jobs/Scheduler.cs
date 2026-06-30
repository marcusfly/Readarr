using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Download;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaCover.Commands;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Update.Commands;
using Timer = System.Timers.Timer;

namespace NzbDrone.Core.Jobs
{
    public class Scheduler :
        IHandle<ApplicationStartedEvent>,
        IHandle<ApplicationShutdownRequested>
    {
        private readonly ITaskManager _taskManager;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IDurableJobScheduler _durableJobScheduler;
        private readonly IDownloadMonitoringCommandSubmitter _downloadMonitoringCommandSubmitter;
        private readonly IImportListSyncCommandSubmitter _importListSyncCommandSubmitter;
        private readonly IRefreshCommandSubmitter _refreshCommandSubmitter;
        private readonly Logger _logger;
        private static readonly Timer Timer = new Timer();
        private static CancellationTokenSource _cancellationTokenSource;

        public Scheduler(ITaskManager taskManager,
                         IManageCommandQueue commandQueueManager,
                         IDurableJobScheduler durableJobScheduler,
                         IDownloadMonitoringCommandSubmitter downloadMonitoringCommandSubmitter,
                         IImportListSyncCommandSubmitter importListSyncCommandSubmitter,
                         IRefreshCommandSubmitter refreshCommandSubmitter,
                         Logger logger)
        {
            _taskManager = taskManager;
            _commandQueueManager = commandQueueManager;
            _durableJobScheduler = durableJobScheduler;
            _downloadMonitoringCommandSubmitter = downloadMonitoringCommandSubmitter;
            _importListSyncCommandSubmitter = importListSyncCommandSubmitter;
            _refreshCommandSubmitter = refreshCommandSubmitter;
            _logger = logger;
        }

        private void ExecuteCommands()
        {
            try
            {
                Timer.Enabled = false;

                var tasks = _taskManager.GetPending().ToList();

                _logger.Trace("Pending Tasks: {0}", tasks.Count);

                foreach (var task in tasks)
                {
                    if (TrySubmitDurableScheduledTask(task))
                    {
                        continue;
                    }

                    _commandQueueManager.Push(task.TypeName, task.LastExecution, task.LastStartTime, task.Priority, CommandTrigger.Scheduled);
                }
            }
            finally
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    Timer.Enabled = true;
                }
            }
        }

        private bool TrySubmitDurableScheduledTask(ScheduledTask task)
        {
            if (task.TypeName == typeof(RefreshMonitoredDownloadsCommand).FullName)
            {
                _downloadMonitoringCommandSubmitter.Submit(
                    new RefreshMonitoredDownloadsCommand
                    {
                        LastExecutionTime = task.LastExecution,
                        LastStartTime = task.LastStartTime,
                        Trigger = CommandTrigger.Scheduled
                    },
                    task.Priority,
                    CommandTrigger.Scheduled);

                return true;
            }

            if (task.TypeName == typeof(RefreshAuthorCommand).FullName)
            {
                _refreshCommandSubmitter.Submit(
                    new RefreshAuthorCommand
                    {
                        LastExecutionTime = task.LastExecution,
                        LastStartTime = task.LastStartTime,
                        Trigger = CommandTrigger.Scheduled
                    },
                    task.Priority,
                    CommandTrigger.Scheduled);

                return true;
            }

            if (task.TypeName == typeof(ImportListSyncCommand).FullName)
            {
                _importListSyncCommandSubmitter.Submit(
                    new ImportListSyncCommand
                    {
                        LastExecutionTime = task.LastExecution,
                        LastStartTime = task.LastStartTime,
                        Trigger = CommandTrigger.Scheduled
                    },
                    task.Priority,
                    CommandTrigger.Scheduled);

                return true;
            }

            if (TrySubmitDurableSimpleScheduledTask(task, out var submitted))
            {
                return submitted;
            }

            return false;
        }

        private bool TrySubmitDurableSimpleScheduledTask(ScheduledTask task, out bool submitted)
        {
            submitted = true;

            switch (task.TypeName)
            {
                case var _ when task.TypeName == typeof(MessagingCleanupCommand).FullName:
                    SubmitScheduled(new MessagingCleanupCommand(), "messaging-cleanup", task);
                    return true;
                case var _ when task.TypeName == typeof(ApplicationUpdateCheckCommand).FullName:
                    SubmitScheduled(new ApplicationUpdateCheckCommand(), "application-update-check:False", task);
                    return true;
                case var _ when task.TypeName == typeof(CheckHealthCommand).FullName:
                    SubmitScheduled(new CheckHealthCommand(), "check-health", task);
                    return true;
                case var _ when task.TypeName == typeof(RescanFoldersCommand).FullName:
                    SubmitScheduled(new RescanFoldersCommand(), "rescan-folders:all", task);
                    return true;
                case var _ when task.TypeName == typeof(HousekeepingCommand).FullName:
                    SubmitScheduled(new HousekeepingCommand(), "housekeeping", task);
                    return true;
                case var _ when task.TypeName == typeof(BackupCommand).FullName:
                    SubmitScheduled(new BackupCommand(), "backup", task);
                    return true;
                case var _ when task.TypeName == typeof(ProcessDeferredCoversCommand).FullName:
                    SubmitScheduled(new ProcessDeferredCoversCommand(), "process-deferred-covers", task);
                    return true;
                case var _ when task.TypeName == typeof(RssSyncCommand).FullName:
                    SubmitScheduled(new RssSyncCommand(), "rss-sync", task);
                    return true;
                default:
                    submitted = false;
                    return true;
            }
        }

        private void SubmitScheduled<TCommand>(TCommand command, string key, ScheduledTask task)
            where TCommand : Command
        {
            command.LastExecutionTime = task.LastExecution;
            command.LastStartTime = task.LastStartTime;
            command.Trigger = CommandTrigger.Scheduled;
            _durableJobScheduler.Submit(command, key, task.Priority, CommandTrigger.Scheduled);
        }

        public void Handle(ApplicationStartedEvent message)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            Timer.Interval = 1000 * 30;
            Timer.Elapsed += (o, args) => Task.Factory.StartNew(ExecuteCommands, _cancellationTokenSource.Token)
                .LogExceptions();

            Timer.Start();
        }

        public void Handle(ApplicationShutdownRequested message)
        {
            _logger.Info("Shutting down scheduler");
            _cancellationTokenSource.Cancel(true);
            Timer.Stop();
        }
    }
}
