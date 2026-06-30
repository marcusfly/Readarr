using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using Readarr.Api.V3.Commands;

namespace NzbDrone.Api.Test.Commands
{
    [TestFixture]
    public class CommandResourceMapperFixture
    {
        [Test]
        public void should_project_queued_durable_attempt_as_synthetic_command_resource()
        {
            var queuedAt = new DateTime(2026, 6, 30, 19, 0, 0, DateTimeKind.Utc);
            var model = new JobAttempt
            {
                Id = 15,
                JobType = typeof(RefreshAuthorCommand).FullName,
                CommandBody = new RefreshAuthorCommand { AuthorId = 123 },
                CommandPriority = CommandPriority.High,
                CommandTrigger = CommandTrigger.Manual,
                State = JobState.Queued,
                QueuedAt = queuedAt
            };

            var resource = model.ToResource();

            resource.Id.Should().Be(-15);
            resource.Name.Should().Be("RefreshAuthor");
            resource.CommandName.Should().Be("Refresh Author");
            resource.Status.Should().Be(CommandStatus.Queued);
            resource.Result.Should().Be(CommandResult.Unknown);
            resource.Priority.Should().Be(CommandPriority.High);
            resource.Trigger.Should().Be(CommandTrigger.Manual);
            resource.Queued.Should().Be(queuedAt);
            resource.Started.Should().BeNull();
            resource.Ended.Should().BeNull();
        }

        [Test]
        public void should_map_canceled_durable_attempt_to_failed_command_shape()
        {
            var startedAt = new DateTime(2026, 6, 30, 19, 5, 0, DateTimeKind.Utc);
            var completedAt = startedAt.AddMinutes(2);
            var model = new JobAttempt
            {
                Id = 16,
                JobType = typeof(RefreshBookCommand).FullName,
                CommandBody = new RefreshBookCommand { BookId = 77 },
                State = JobState.Canceled,
                QueuedAt = startedAt.AddMinutes(-1),
                StartedAt = startedAt,
                CompletedAt = completedAt
            };

            var resource = model.ToResource();

            resource.Status.Should().Be(CommandStatus.Failed);
            resource.Result.Should().Be(CommandResult.Unsuccessful);
            resource.Exception.Should().Be("Canceled");
            resource.Started.Should().Be(startedAt);
            resource.Ended.Should().Be(completedAt);
            resource.Duration.Should().Be(TimeSpan.FromMinutes(2));
        }
    }
}
