using System;
using NLog;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

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
    public class DurableJobScheduler : IHandle<ApplicationStartedEvent>,
                                       IHandle<CommandExecutedEvent>,
                                       IJobProgressReporter
    {
        private const int DefaultMaxAttempts = 3;

        private readonly IJobAttemptService _jobAttemptService;
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly Logger _logger;

        // Per-thread reference to the active JobAttempt so that progress
        // reports can be routed back without passing the context through every
        // call site.
        [ThreadStatic]
        private static JobAttempt _currentAttempt;

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
            var attempt = _jobAttemptService.Submit(typeof(TCommand).FullName, idempotencyKey);

            // Only dispatch when we just created a new Queued attempt.
            if (attempt.State != JobState.Queued || attempt.CommandId.HasValue)
            {
                return attempt;
            }

            var leaseToken = Guid.NewGuid();
            _currentAttempt = attempt;
            try
            {
                var commandModel = _commandQueueManager.Push(command, priority, trigger);

                // Record the CommandModel id so CommandExecutedEvent can find us.
                _jobAttemptService.MarkRunning(attempt, leaseToken, commandModel.Id);
            }
            finally
            {
                _currentAttempt = null;
            }

            return attempt;
        }

        // ------------------------------------------------------------------ //
        //  IJobProgressReporter
        // ------------------------------------------------------------------ //
        public void ReportProgress(int percent)
        {
            var attempt = _currentAttempt;
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
            else if (message.Command.Status == CommandStatus.Failed)
            {
                _jobAttemptService.MarkFailed(attempt,
                    message.Command.Exception ?? "Unknown failure",
                    DefaultMaxAttempts);
            }
        }
    }
}
