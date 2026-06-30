using System;
using System.Linq;
using NLog;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ProgressMessaging;

namespace NzbDrone.Core.Jobs.Durable
{
    /// <summary>
    /// Wraps the existing IManageCommandQueue with durable persistence.
    /// The existing CommandExecutor drives actual execution; this layer adds an
    /// idempotent JobAttempt record whose lifecycle mirrors the CommandModel.
    ///
    /// Correlation between a JobAttempt and the CommandModel is stored via
    /// JobAttempt.CommandId so that the CommandExecutedEvent handler can find
    /// the right record even when multiple attempts of the same job type are in
    /// flight concurrently.
    ///
    /// On application start, any attempts left in Running state (leaked from a
    /// previous process) are transitioned to Retrying.
    /// </summary>
    public class DurableJobScheduler : IDurableJobScheduler,
                                       IHandle<ApplicationStartedEvent>,
                                       IHandle<CommandExecutedEvent>,
                                       IJobProgressReporter
    {
        private const int DefaultMaxAttempts = 3;

        private readonly IJobAttemptService _jobAttemptService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        public DurableJobScheduler(IJobAttemptService jobAttemptService,
                                   IManageCommandQueue commandQueueManager,
                                   Logger logger)
        {
            _jobAttemptService = jobAttemptService;
            _commandQueueManager = commandQueueManager;
            _logger = logger;
        }

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Submit a durable job that wraps a command.  If an attempt for the
        /// same idempotency key is already Queued or Running the call is a
        /// no-op and the existing record is returned without pushing a duplicate
        /// command into the pipeline.
        /// </summary>
        public JobAttempt Submit<TCommand>(TCommand command,
                                           string idempotencyKey,
                                           CommandPriority priority = CommandPriority.Normal,
                                           CommandTrigger trigger = CommandTrigger.Unspecified)
            where TCommand : Command
        {
            var attempt = _jobAttemptService.Submit(command, typeof(TCommand).FullName, idempotencyKey, priority, trigger);

            // Only dispatch when we just created a new Queued attempt.
            if (attempt.State != JobState.Queued || attempt.CommandId.HasValue)
            {
                return attempt;
            }

            var leaseToken = Guid.NewGuid();
            var commandModel = _commandQueueManager.Push(command, priority, trigger);

            // Record the CommandModel id so CommandExecutedEvent can find us.
            _jobAttemptService.MarkRunning(attempt, leaseToken, commandModel.Id);

            return attempt;
        }

        // ------------------------------------------------------------------ //
        //  IJobProgressReporter
        // ------------------------------------------------------------------ //
        public void ReportProgress(int percent)
        {
            var command = ProgressMessageContext.CommandModel;
            if (command == null)
            {
                return;
            }

            var attempt = _jobAttemptService.FindByCommandId(command.Id);
            if (attempt == null)
            {
                return;
            }

            var clamped = Math.Max(0, Math.Min(100, percent));
            _jobAttemptService.UpdateProgress(attempt, clamped);
        }

        // ------------------------------------------------------------------ //
        //  Event handlers
        // ------------------------------------------------------------------ //
        public void Handle(ApplicationStartedEvent message)
        {
            var stuck = _jobAttemptService.GetStuckRunning();

            foreach (var attempt in stuck)
            {
                _logger.Warn("Requeueing stuck durable job {0} (id={1})", attempt.JobType, attempt.Id);
                _jobAttemptService.RequeueStuck(attempt);
            }

            var pendingReplay = _jobAttemptService.GetPendingReplay();

            foreach (var attempt in pendingReplay)
            {
                Replay(attempt);
            }
        }

        public void Handle(CommandExecutedEvent message)
        {
            var commandId = message.Command.Id;
            var attempt = _jobAttemptService.FindByCommandId(commandId);

            if (attempt == null)
            {
                // This command was not submitted through the durable scheduler.
                return;
            }

            if (message.Command.Status == CommandStatus.Completed)
            {
                _jobAttemptService.MarkCompleted(attempt);
            }
            else if (message.Command.Status == CommandStatus.Cancelled)
            {
                _jobAttemptService.MarkCanceled(attempt);
            }
            else if (message.Command.Status == CommandStatus.Failed)
            {
                _jobAttemptService.MarkFailed(attempt,
                    message.Command.Exception ?? "Unknown failure",
                    DefaultMaxAttempts);
            }
        }

        private void Replay(JobAttempt attempt)
        {
            if (!CanReplay(attempt))
            {
                _logger.Warn("Skipping durable job replay for {0} (id={1}) because no supported command metadata was persisted", attempt.JobType, attempt.Id);
                return;
            }

            _logger.Info("Replaying durable job {0} (id={1})", attempt.JobType, attempt.Id);

            var leaseToken = Guid.NewGuid();
            var commandModel = PushReplayCommand(attempt.CommandBody, attempt.CommandPriority, attempt.CommandTrigger);
            _jobAttemptService.MarkRunning(attempt, leaseToken, commandModel.Id);
        }

        private static bool CanReplay(JobAttempt attempt)
        {
            return attempt.CommandBody != null && !(attempt.CommandBody is UnknownCommand);
        }

        private CommandModel PushReplayCommand(Command command, CommandPriority priority, CommandTrigger trigger)
        {
            var pushMethod = typeof(IManageCommandQueue).GetMethods()
                .Single(m => m.Name == nameof(IManageCommandQueue.Push) &&
                             m.IsGenericMethodDefinition &&
                             m.GetParameters().Length == 3);

            var closedMethod = pushMethod.MakeGenericMethod(command.GetType());
            return (CommandModel)closedMethod.Invoke(_commandQueueManager, new object[] { command, priority, trigger });
        }
    }
}
