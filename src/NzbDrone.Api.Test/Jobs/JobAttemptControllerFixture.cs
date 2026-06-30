using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Test.Common;
using Readarr.Api.V3.Jobs;

namespace NzbDrone.Api.Test.Jobs
{
    [TestFixture]
    public class JobAttemptControllerFixture : TestBase<JobAttemptController>
    {
        [Test]
        public void get_all_should_return_all_attempts_when_state_is_not_specified()
        {
            var queuedAt = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetAll())
                  .Returns(new List<JobAttempt>
                  {
                      new JobAttempt
                      {
                          Id = 7,
                          JobType = "RefreshAuthor",
                          IdempotencyKey = "refresh-author-7",
                          CommandId = 42,
                          State = JobState.Running,
                          QueuedAt = queuedAt,
                          StartedAt = queuedAt.AddMinutes(1),
                          AttemptCount = 1,
                          Progress = 35
                      }
                  });

            var result = Subject.GetAll();

            result.Should().ContainSingle();
            result[0].Id.Should().Be(7);
            result[0].JobType.Should().Be("RefreshAuthor");
            result[0].IdempotencyKey.Should().Be("refresh-author-7");
            result[0].CommandId.Should().Be(42);
            result[0].State.Should().Be(JobState.Running);
            result[0].QueuedAt.Should().Be(queuedAt);
            result[0].Progress.Should().Be(35);

            Mocker.GetMock<IJobAttemptService>().Verify(s => s.GetAll(), Times.Once());
            Mocker.GetMock<IJobAttemptService>().Verify(s => s.GetByState(It.IsAny<JobState>()), Times.Never());
        }

        [Test]
        public void get_all_should_filter_by_state_when_requested()
        {
            Mocker.GetMock<IJobAttemptService>()
                  .Setup(s => s.GetByState(JobState.Failed))
                  .Returns(new List<JobAttempt>
                  {
                      new JobAttempt
                      {
                          Id = 8,
                          JobType = "RefreshBook",
                          IdempotencyKey = "refresh-book-8",
                          CommandId = 88,
                          State = JobState.Failed,
                          QueuedAt = DateTime.UtcNow,
                          AttemptCount = 3,
                          LastError = "boom"
                      }
                  });

            var result = Subject.GetAll(JobState.Failed);

            result.Should().ContainSingle();
            result[0].State.Should().Be(JobState.Failed);
            result[0].CommandId.Should().Be(88);
            result[0].LastError.Should().Be("boom");

            Mocker.GetMock<IJobAttemptService>().Verify(s => s.GetByState(JobState.Failed), Times.Once());
            Mocker.GetMock<IJobAttemptService>().Verify(s => s.GetAll(), Times.Never());
        }
    }
}
