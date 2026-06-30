using System;
using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ProgressMessaging;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.JobTests.Durable
{
    [TestFixture]
    public class DurableJobSchedulerFixture : CoreTest<DurableJobScheduler>
    {
        [Test]
        public void on_application_started_requeues_stuck_running_jobs()
        {
            var stuck = new List<JobAttempt>
            {
                new JobAttempt { Id = 1, JobType = "SomeJob", State = JobState.Running, LeaseToken = Guid.NewGuid() }
            };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetStuckRunning())
                  .Returns(stuck);

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetPendingReplay())
                  .Returns(new List<JobAttempt>());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.RequeueStuck(stuck[0]), Times.Once());
        }

        [Test]
        public void on_application_started_does_nothing_when_no_stuck_jobs()
        {
            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetStuckRunning())
                  .Returns(new List<JobAttempt>());

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetPendingReplay())
                  .Returns(new List<JobAttempt>());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.RequeueStuck(It.IsAny<JobAttempt>()), Times.Never());
        }

        [Test]
        public void on_application_started_replays_pending_durable_commands()
        {
            var attempt = new JobAttempt
            {
                Id = 5,
                JobType = typeof(TestReplayCommand).FullName,
                State = JobState.Retrying,
                CommandBody = new TestReplayCommand { AuthorId = 42 },
                CommandPriority = CommandPriority.High,
                CommandTrigger = CommandTrigger.Manual
            };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetStuckRunning())
                  .Returns(new List<JobAttempt>());

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetPendingReplay())
                  .Returns(new List<JobAttempt> { attempt });

            Mocker.GetMock<IManageCommandQueue>()
                  .Setup(s => s.Push(It.IsAny<TestReplayCommand>(), CommandPriority.High, CommandTrigger.Manual))
                  .Returns(new CommandModel { Id = 55, Body = new TestReplayCommand { AuthorId = 42 } });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(s => s.Push(It.Is<TestReplayCommand>(c => c.AuthorId == 42), CommandPriority.High, CommandTrigger.Manual), Times.Once());

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkRunning(attempt, It.IsAny<Guid>(), 55), Times.Once());
        }

        [Test]
        public void on_application_started_skips_legacy_attempts_without_replayable_command_metadata()
        {
            var attempt = new JobAttempt
            {
                Id = 6,
                JobType = "LegacyJob",
                State = JobState.Retrying,
                CommandBody = new UnknownCommand { ContractName = "MissingCommand" }
            };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetStuckRunning())
                  .Returns(new List<JobAttempt>());

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetPendingReplay())
                  .Returns(new List<JobAttempt> { attempt });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(s => s.Push(It.IsAny<TestReplayCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkRunning(It.IsAny<JobAttempt>(), It.IsAny<Guid>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void handle_command_executed_marks_attempt_completed_on_success()
        {
            const int commandId = 42;
            var attempt = new JobAttempt { Id = 2, CommandId = commandId, State = JobState.Running };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.FindByCommandId(commandId))
                  .Returns(attempt);

            var commandModel = BuildCommandModel(commandId, CommandStatus.Completed);

            Subject.Handle(new CommandExecutedEvent(commandModel));

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkCompleted(attempt), Times.Once());
        }

        [Test]
        public void handle_command_executed_marks_attempt_failed_on_failure()
        {
            const int commandId = 43;
            var attempt = new JobAttempt { Id = 3, CommandId = commandId, State = JobState.Running, AttemptCount = 3 };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.FindByCommandId(commandId))
                  .Returns(attempt);

            var commandModel = BuildCommandModel(commandId, CommandStatus.Failed, "something failed");

            Subject.Handle(new CommandExecutedEvent(commandModel));

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkFailed(attempt, It.IsAny<string>(), It.IsAny<int>()), Times.Once());
        }

        [Test]
        public void handle_command_executed_marks_attempt_canceled_on_cancellation()
        {
            const int commandId = 44;
            var attempt = new JobAttempt { Id = 4, CommandId = commandId, State = JobState.Running };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.FindByCommandId(commandId))
                  .Returns(attempt);

            var commandModel = BuildCommandModel(commandId, CommandStatus.Cancelled);

            Subject.Handle(new CommandExecutedEvent(commandModel));

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkCanceled(attempt), Times.Once());
        }

        [Test]
        public void handle_command_executed_does_nothing_when_no_matching_attempt()
        {
            const int commandId = 99;

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.FindByCommandId(commandId))
                  .Returns((JobAttempt)null);

            var commandModel = BuildCommandModel(commandId, CommandStatus.Completed);

            Subject.Handle(new CommandExecutedEvent(commandModel));

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkCompleted(It.IsAny<JobAttempt>()), Times.Never());
            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.MarkFailed(It.IsAny<JobAttempt>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void report_progress_is_noop_when_no_active_command_context_exists()
        {
            Subject.ReportProgress(50);

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.UpdateProgress(It.IsAny<JobAttempt>(), It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void report_progress_updates_attempt_for_active_command_context()
        {
            var attempt = new JobAttempt { Id = 12, CommandId = 55, State = JobState.Running };
            ProgressMessageContext.CommandModel = new CommandModel
            {
                Id = 55,
                Body = new TestCommand()
            };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.FindByCommandId(55))
                  .Returns(attempt);

            try
            {
                Subject.ReportProgress(125);
            }
            finally
            {
                ProgressMessageContext.CommandModel = null;
            }

            Mocker.GetMock<IJobAttemptService>()
                  .Verify(s => s.UpdateProgress(attempt, 100), Times.Once());
        }

        // ------------------------------------------------------------------ //
        //  Helpers
        // ------------------------------------------------------------------ //
        private static CommandModel BuildCommandModel(int id,
                                                      CommandStatus status,
                                                      string exception = null)
        {
            return new CommandModel
            {
                Id = id,
                Name = "Test",
                Body = new TestCommand(),
                Status = status,
                Exception = exception,
                QueuedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow
            };
        }

        private class TestCommand : Command
        {
        }

        private class TestReplayCommand : Command
        {
            public int AuthorId { get; set; }
        }
    }
}
