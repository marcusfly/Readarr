using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Jobs.Durable;
using Readarr.Api.V3.Jobs;

namespace NzbDrone.Api.Test.Jobs
{
    [TestFixture]
    public class JobAttemptResourceMapperFixture
    {
        [Test]
        public void to_resource_should_map_command_correlation_and_durable_fields()
        {
            var queuedAt = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);
            var startedAt = queuedAt.AddMinutes(2);
            var completedAt = startedAt.AddMinutes(5);

            var model = new JobAttempt
            {
                Id = 9,
                JobType = "RefreshAuthor",
                IdempotencyKey = "refresh-author-9",
                CommandId = 123,
                State = JobState.Completed,
                QueuedAt = queuedAt,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                AttemptCount = 2,
                LastError = "previous failure",
                Progress = 100
            };

            var resource = model.ToResource();

            resource.Id.Should().Be(9);
            resource.JobType.Should().Be("RefreshAuthor");
            resource.IdempotencyKey.Should().Be("refresh-author-9");
            resource.CommandId.Should().Be(123);
            resource.State.Should().Be(JobState.Completed);
            resource.QueuedAt.Should().Be(queuedAt);
            resource.StartedAt.Should().Be(startedAt);
            resource.CompletedAt.Should().Be(completedAt);
            resource.AttemptCount.Should().Be(2);
            resource.LastError.Should().Be("previous failure");
            resource.Progress.Should().Be(100);
        }

        [Test]
        public void to_resource_should_return_null_for_null_model()
        {
            JobAttempt model = null;

            model.ToResource().Should().BeNull();
        }
    }
}
