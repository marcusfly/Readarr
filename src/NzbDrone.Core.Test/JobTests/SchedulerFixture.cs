using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Download;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.JobTests
{
    [TestFixture]
    public class SchedulerFixture : CoreTest<Scheduler>
    {
        [SetUp]
        public void SetUp()
        {
            var cancellationTokenSourceField = typeof(Scheduler).GetField("_cancellationTokenSource", BindingFlags.NonPublic | BindingFlags.Static);
            cancellationTokenSourceField.SetValue(null, new CancellationTokenSource());
        }

        [Test]
        public void should_submit_scheduled_full_refresh_author_through_durable_submitter()
        {
            var task = new ScheduledTask
            {
                TypeName = typeof(RefreshAuthorCommand).FullName,
                LastExecution = new DateTime(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc),
                LastStartTime = new DateTime(2026, 6, 30, 10, 5, 0, DateTimeKind.Utc),
                Priority = CommandPriority.High
            };

            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetPending())
                  .Returns(new List<ScheduledTask> { task });

            InvokeExecuteCommands();

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(
                      It.Is<RefreshAuthorCommand>(c =>
                          !c.AuthorId.HasValue &&
                          c.LastExecutionTime == task.LastExecution &&
                          c.LastStartTime == task.LastStartTime &&
                          c.Trigger == CommandTrigger.Scheduled),
                      CommandPriority.High,
                      CommandTrigger.Scheduled),
                      Times.Once());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_submit_scheduled_refresh_monitored_downloads_through_durable_submitter()
        {
            var task = new ScheduledTask
            {
                TypeName = typeof(RefreshMonitoredDownloadsCommand).FullName,
                LastExecution = new DateTime(2026, 6, 30, 9, 0, 0, DateTimeKind.Utc),
                LastStartTime = new DateTime(2026, 6, 30, 9, 1, 0, DateTimeKind.Utc),
                Priority = CommandPriority.High
            };

            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetPending())
                  .Returns(new List<ScheduledTask> { task });

            InvokeExecuteCommands();

            Mocker.GetMock<IDownloadMonitoringCommandSubmitter>()
                  .Verify(v => v.Submit(
                      It.Is<RefreshMonitoredDownloadsCommand>(c =>
                          c.LastExecutionTime == task.LastExecution &&
                          c.LastStartTime == task.LastStartTime &&
                          c.Trigger == CommandTrigger.Scheduled),
                      CommandPriority.High,
                      CommandTrigger.Scheduled),
                      Times.Once());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_submit_scheduled_import_list_sync_through_durable_submitter()
        {
            var task = new ScheduledTask
            {
                TypeName = typeof(ImportListSyncCommand).FullName,
                LastExecution = new DateTime(2026, 6, 30, 8, 0, 0, DateTimeKind.Utc),
                LastStartTime = new DateTime(2026, 6, 30, 8, 2, 0, DateTimeKind.Utc),
                Priority = CommandPriority.Normal
            };

            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetPending())
                  .Returns(new List<ScheduledTask> { task });

            InvokeExecuteCommands();

            Mocker.GetMock<IImportListSyncCommandSubmitter>()
                  .Verify(v => v.Submit(
                      It.Is<ImportListSyncCommand>(c =>
                          !c.DefinitionId.HasValue &&
                          c.LastExecutionTime == task.LastExecution &&
                          c.LastStartTime == task.LastStartTime &&
                          c.Trigger == CommandTrigger.Scheduled),
                      CommandPriority.Normal,
                      CommandTrigger.Scheduled),
                      Times.Once());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_submit_scheduled_backup_through_durable_scheduler()
        {
            var task = new ScheduledTask
            {
                TypeName = typeof(BackupCommand).FullName,
                LastExecution = new DateTime(2026, 6, 30, 11, 0, 0, DateTimeKind.Utc),
                LastStartTime = new DateTime(2026, 6, 30, 11, 1, 0, DateTimeKind.Utc),
                Priority = CommandPriority.Normal
            };

            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetPending())
                  .Returns(new List<ScheduledTask> { task });

            InvokeExecuteCommands();

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(
                      It.Is<BackupCommand>(c =>
                          c.LastExecutionTime == task.LastExecution &&
                          c.LastStartTime == task.LastStartTime &&
                          c.Trigger == CommandTrigger.Scheduled),
                      "backup",
                      CommandPriority.Normal,
                      CommandTrigger.Scheduled),
                      Times.Once());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_continue_using_legacy_command_queue_for_unknown_scheduled_tasks()
        {
            var task = new ScheduledTask
            {
                TypeName = typeof(SchedulerFixture).FullName,
                LastExecution = new DateTime(2026, 6, 30, 11, 0, 0, DateTimeKind.Utc),
                LastStartTime = new DateTime(2026, 6, 30, 11, 1, 0, DateTimeKind.Utc),
                Priority = CommandPriority.Normal
            };

            Mocker.GetMock<ITaskManager>()
                  .Setup(v => v.GetPending())
                  .Returns(new List<ScheduledTask> { task });

            InvokeExecuteCommands();

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(v => v.Push(task.TypeName, task.LastExecution, task.LastStartTime, task.Priority, CommandTrigger.Scheduled), Times.Once());
        }

        private void InvokeExecuteCommands()
        {
            var executeCommands = typeof(Scheduler).GetMethod("ExecuteCommands", BindingFlags.Instance | BindingFlags.NonPublic);
            executeCommands.Invoke(Subject, null);
        }
    }
}
