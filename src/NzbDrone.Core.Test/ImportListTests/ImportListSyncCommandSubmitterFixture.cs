using System;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]
    public class ImportListSyncCommandSubmitterFixture : CoreTest<ImportListSyncCommandSubmitter>
    {
        [Test]
        public void should_submit_full_sync_with_stable_all_idempotency_key()
        {
            var command = new ImportListSyncCommand();

            Subject.Submit(command, CommandPriority.High, CommandTrigger.Manual);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "import-list-sync:all", CommandPriority.High, CommandTrigger.Manual), Times.Once());
        }

        [Test]
        public void should_submit_definition_sync_with_stable_definition_key()
        {
            var command = new ImportListSyncCommand(37);

            Subject.Submit(command);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "import-list-sync:37", CommandPriority.Normal, CommandTrigger.Unspecified), Times.Once());
        }

        [Test]
        public void should_preserve_scheduled_task_timing_metadata_on_full_sync_command()
        {
            var command = new ImportListSyncCommand
            {
                LastExecutionTime = new DateTime(2026, 6, 30, 10, 0, 0, DateTimeKind.Utc),
                LastStartTime = new DateTime(2026, 6, 30, 10, 5, 0, DateTimeKind.Utc),
                Trigger = CommandTrigger.Scheduled
            };

            Subject.Submit(command, CommandPriority.Low, CommandTrigger.Scheduled);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(
                      It.Is<ImportListSyncCommand>(c =>
                          !c.DefinitionId.HasValue &&
                          c.LastExecutionTime == command.LastExecutionTime &&
                          c.LastStartTime == command.LastStartTime &&
                          c.Trigger == CommandTrigger.Scheduled),
                      "import-list-sync:all",
                      CommandPriority.Low,
                      CommandTrigger.Scheduled),
                      Times.Once());
        }
    }
}
