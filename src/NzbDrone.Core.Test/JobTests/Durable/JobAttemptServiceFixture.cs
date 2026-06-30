using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.JobTests.Durable
{
    [TestFixture]
    public class JobAttemptServiceFixture : CoreTest<JobAttemptService>
    {
        private const string JobType = "NzbDrone.Core.Books.Services.RefreshAuthorService";
        private const string Key = "refresh-author-42";

        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.FindByIdempotencyKey(It.IsAny<string>()))
                  .Returns((JobAttempt)null);
        }

        [Test]
        public void submit_inserts_new_attempt_when_no_existing_key()
        {
            var command = new ReplayableCommand { AuthorId = 42 };

            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.Insert(It.IsAny<JobAttempt>()))
                  .Returns<JobAttempt>(a =>
                      {
                          a.Id = 1;
                          return a;
                      });

            var result = Subject.Submit(command, JobType, Key, CommandPriority.High, CommandTrigger.Manual);

            result.State.Should().Be(JobState.Queued);
            result.AttemptCount.Should().Be(0);
            result.IdempotencyKey.Should().Be(Key);
            result.CommandBody.Should().BeSameAs(command);
            result.CommandPriority.Should().Be(CommandPriority.High);
            result.CommandTrigger.Should().Be(CommandTrigger.Manual);

            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == result)), Times.Once());
        }

        [Test]
        public void submit_is_noop_when_existing_attempt_is_queued()
        {
            var existing = new JobAttempt
            {
                Id = 7,
                JobType = JobType,
                IdempotencyKey = Key,
                State = JobState.Queued
            };

            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.FindByIdempotencyKey(Key))
                  .Returns(existing);

            var result = Subject.Submit(new ReplayableCommand(), JobType, Key, CommandPriority.Normal, CommandTrigger.Unspecified);

            result.Id.Should().Be(7);
            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Never());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DurableJobUpdatedEvent>()), Times.Never());
        }

        [Test]
        public void submit_is_noop_when_existing_attempt_is_running()
        {
            var existing = new JobAttempt
            {
                Id = 8,
                JobType = JobType,
                IdempotencyKey = Key,
                State = JobState.Running
            };

            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.FindByIdempotencyKey(Key))
                  .Returns(existing);

            var result = Subject.Submit(new ReplayableCommand(), JobType, Key, CommandPriority.Normal, CommandTrigger.Unspecified);

            result.Id.Should().Be(8);
            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Never());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DurableJobUpdatedEvent>()), Times.Never());
        }

        [Test]
        public void submit_creates_new_attempt_when_existing_is_failed()
        {
            var existing = new JobAttempt
            {
                Id = 9,
                JobType = JobType,
                IdempotencyKey = Key,
                State = JobState.Failed
            };

            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.FindByIdempotencyKey(Key))
                  .Returns(existing);

            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.Insert(It.IsAny<JobAttempt>()))
                  .Returns<JobAttempt>(a =>
                  {
                      a.Id = 10;
                      return a;
                  });

            var result = Subject.Submit(new ReplayableCommand(), JobType, Key, CommandPriority.Normal, CommandTrigger.Unspecified);

            result.State.Should().Be(JobState.Queued);
            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == result)), Times.Once());
        }

        [Test]
        public void mark_failed_sets_retrying_when_under_max_attempts()
        {
            var attempt = new JobAttempt { AttemptCount = 1 };

            Subject.MarkFailed(attempt, "boom", 3);

            attempt.State.Should().Be(JobState.Retrying);
            attempt.LastError.Should().Be("boom");
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == attempt)), Times.Once());
        }

        [Test]
        public void mark_failed_sets_failed_when_at_max_attempts()
        {
            var attempt = new JobAttempt { AttemptCount = 3 };

            Subject.MarkFailed(attempt, "boom", 3);

            attempt.State.Should().Be(JobState.Failed);
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == attempt)), Times.Once());
        }

        [Test]
        public void mark_completed_sets_progress_to_100()
        {
            var attempt = new JobAttempt { Id = 1, Progress = 50 };

            Subject.MarkCompleted(attempt);

            attempt.State.Should().Be(JobState.Completed);
            attempt.Progress.Should().Be(100);
            attempt.CompletedAt.Should().NotBeNull();
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == attempt)), Times.Once());
        }

        [Test]
        public void mark_running_clears_previous_terminal_fields_and_resets_progress()
        {
            var attempt = new JobAttempt
            {
                Id = 11,
                State = JobState.Retrying,
                CompletedAt = DateTime.UtcNow.AddMinutes(-1),
                LastError = "boom",
                Progress = 75,
                AttemptCount = 1
            };

            Subject.MarkRunning(attempt, Guid.NewGuid(), 42);

            attempt.State.Should().Be(JobState.Running);
            attempt.CommandId.Should().Be(42);
            attempt.CompletedAt.Should().BeNull();
            attempt.LastError.Should().BeNull();
            attempt.Progress.Should().Be(0);
            attempt.AttemptCount.Should().Be(2);
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == attempt)), Times.Once());
        }

        [Test]
        public void mark_canceled_sets_terminal_state_and_publishes_update()
        {
            var attempt = new JobAttempt { Id = 21, State = JobState.Running };

            Subject.MarkCanceled(attempt);

            attempt.State.Should().Be(JobState.Canceled);
            attempt.CompletedAt.Should().NotBeNull();
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == attempt)), Times.Once());
        }

        [Test]
        public void update_progress_does_not_publish_durable_update_event()
        {
            var attempt = new JobAttempt { Id = 1, Progress = 50 };

            Subject.UpdateProgress(attempt, 75);

            attempt.Progress.Should().Be(75);
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DurableJobUpdatedEvent>()), Times.Never());
        }

        [Test]
        public void requeue_stuck_sets_state_to_retrying_and_clears_lease()
        {
            var attempt = new JobAttempt
            {
                Id = 5,
                State = JobState.Running,
                LeaseToken = Guid.NewGuid()
            };

            Subject.RequeueStuck(attempt);

            attempt.State.Should().Be(JobState.Retrying);
            attempt.LeaseToken.Should().BeNull();
            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.Is<DurableJobUpdatedEvent>(e => e.JobAttempt == attempt)), Times.Once());
        }

        [Test]
        public void get_pending_replay_returns_retrying_and_commandless_queued_attempts()
        {
            var attempts = new System.Collections.Generic.List<JobAttempt>
            {
                new JobAttempt { Id = 11, State = JobState.Retrying },
                new JobAttempt { Id = 12, State = JobState.Queued, CommandId = null }
            };

            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.GetPendingReplay())
                  .Returns(attempts);

            Subject.GetPendingReplay().Should().BeEquivalentTo(attempts);
        }

        private class ReplayableCommand : Command
        {
            public int AuthorId { get; set; }
        }
    }
}
