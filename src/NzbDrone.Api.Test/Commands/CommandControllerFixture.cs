using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Composition;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.SignalR;
using NzbDrone.Test.Common;
using Readarr.Api.V3.Commands;

namespace NzbDrone.Api.Test.Commands
{
    [TestFixture]
    public class CommandControllerFixture : TestBase<CommandController>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.SetConstant(new KnownTypes(new List<Type>()));
            Mocker.GetMock<IBroadcastSignalRMessage>()
                  .SetupGet(v => v.IsConnected)
                  .Returns(false);
        }

        [Test]
        public void get_started_commands_should_merge_active_durable_attempts_without_duplicate_rows()
        {
            var liveCommand = new CommandModel
            {
                Id = 42,
                Name = "RefreshAuthor",
                Body = new RefreshAuthorCommand { AuthorId = 10 },
                Priority = CommandPriority.Normal,
                Status = CommandStatus.Queued,
                QueuedAt = new DateTime(2026, 6, 30, 18, 0, 0, DateTimeKind.Utc)
            };

            Mocker.GetMock<IManageCommandQueue>()
                  .Setup(v => v.All())
                  .Returns(new List<CommandModel> { liveCommand });

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(v => v.GetAll())
                  .Returns(new List<JobAttempt>
                  {
                      new JobAttempt
                      {
                          Id = 7,
                          JobType = typeof(RefreshAuthorCommand).FullName,
                          CommandBody = new RefreshAuthorCommand { AuthorId = 10 },
                          CommandPriority = CommandPriority.Normal,
                          CommandTrigger = CommandTrigger.Manual,
                          State = JobState.Queued,
                          QueuedAt = new DateTime(2026, 6, 30, 18, 1, 0, DateTimeKind.Utc)
                      },
                      new JobAttempt
                      {
                          Id = 8,
                          JobType = typeof(RefreshBookCommand).FullName,
                          CommandBody = new RefreshBookCommand { BookId = 99 },
                          CommandPriority = CommandPriority.High,
                          CommandTrigger = CommandTrigger.Manual,
                          State = JobState.Queued,
                          QueuedAt = new DateTime(2026, 6, 30, 18, 2, 0, DateTimeKind.Utc)
                      }
                  });

            var result = Subject.GetStartedCommands();

            result.Should().HaveCount(2);
            result.Should().ContainSingle(v => v.Id == 42 && v.Name == "RefreshAuthor");
            result.Should().ContainSingle(v => v.Id == -8 &&
                                               v.Name == "RefreshBook" &&
                                               v.Status == CommandStatus.Queued &&
                                               v.Priority == CommandPriority.High);
        }

        [Test]
        public void cancel_command_should_cancel_queued_durable_only_attempts()
        {
            var attempt = new JobAttempt
            {
                Id = 12,
                State = JobState.Queued,
                CommandBody = new RefreshAuthorCommand { AuthorId = 22 }
            };

            Mocker.GetMock<IJobAttemptService>()
                  .Setup(v => v.GetById(12))
                  .Returns(attempt);

            Subject.CancelCommand(-12);

            Mocker.GetMock<IJobAttemptService>().Verify(v => v.MarkCanceled(attempt), Times.Once());
            Mocker.GetMock<IManageCommandQueue>().Verify(v => v.Cancel(It.IsAny<int>()), Times.Never());
        }

        [Test]
        public void cancel_command_should_delegate_live_ids_to_command_queue()
        {
            Subject.CancelCommand(55);

            Mocker.GetMock<IManageCommandQueue>().Verify(v => v.Cancel(55), Times.Once());
            Mocker.GetMock<IJobAttemptService>().Verify(v => v.MarkCanceled(It.IsAny<JobAttempt>()), Times.Never());
        }
    }
}
