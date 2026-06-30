using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Jobs;
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
        public void should_continue_using_legacy_command_queue_for_other_scheduled_tasks()
        {
            var task = new ScheduledTask
            {
                TypeName = typeof(MessagingCleanupCommand).FullName,
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

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.IsAny<RefreshAuthorCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        private void InvokeExecuteCommands()
        {
            var executeCommands = typeof(Scheduler).GetMethod("ExecuteCommands", BindingFlags.Instance | BindingFlags.NonPublic);
            executeCommands.Invoke(Subject, null);
        }
    }
}
