using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Jobs.Durable;
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
            Mocker.GetMock<IJobAttemptRepository>()
                  .Setup(r => r.Insert(It.IsAny<JobAttempt>()))
                  .Returns<JobAttempt>(a => { a.Id = 1; return a; });

            var result = Subject.Submit(JobType, Key);

            result.State.Should().Be(JobState.Queued);
            result.AttemptCount.Should().Be(0);
            result.IdempotencyKey.Should().Be(Key);

            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Once());
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

            var result = Subject.Submit(JobType, Key);

            result.Id.Should().Be(7);
            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Never());
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

            var result = Subject.Submit(JobType, Key);

            result.Id.Should().Be(8);
            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Never());
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
                  .Returns<JobAttempt>(a => { a.Id = 10; return a; });

            var result = Subject.Submit(JobType, Key);

            result.State.Should().Be(JobState.Queued);
            Mocker.GetMock<IJobAttemptRepository>()
                  .Verify(r => r.Insert(It.IsAny<JobAttempt>()), Times.Once());
        }

        [Test]
        public void mark_failed_sets_retrying_when_under_max_attempts()
        {
            var attempt = new JobAttempt { AttemptCount = 1 };

            Subject.MarkFailed(attempt, "boom", 3);

            attempt.State.Should().Be(JobState.Retrying);
            attempt.LastError.Should().Be("boom");
        }

        [Test]
        public void mark_failed_sets_failed_when_at_max_attempts()
        {
            var attempt = new JobAttempt { AttemptCount = 3 };

            Subject.MarkFailed(attempt, "boom", 3);

            attempt.State.Should().Be(JobState.Failed);
        }

        [Test]
        public void mark_completed_sets_progress_to_100()
        {
            var attempt = new JobAttempt { Id = 1, Progress = 50 };

            Subject.MarkCompleted(attempt);

            attempt.State.Should().Be(JobState.Completed);
            attempt.Progress.Should().Be(100);
            attempt.CompletedAt.Should().NotBeNull();
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
        }
    }
}
