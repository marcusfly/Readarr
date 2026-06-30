using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Commands
{
    public class CommandResource : RestResource
    {
        public string Name { get; set; }
        public string CommandName { get; set; }
        public string Message { get; set; }
        public Command Body { get; set; }
        public CommandPriority Priority { get; set; }
        public CommandStatus Status { get; set; }
        public CommandResult Result { get; set; }
        public DateTime Queued { get; set; }
        public DateTime? Started { get; set; }
        public DateTime? Ended { get; set; }
        public TimeSpan? Duration { get; set; }
        public string Exception { get; set; }
        public CommandTrigger Trigger { get; set; }
        public int? JobId { get; set; }
        public JobState? JobState { get; set; }
        public int? JobProgress { get; set; }
        public int? JobAttemptCount { get; set; }
        public string JobLastError { get; set; }

        public string ClientUserAgent { get; set; }

        [JsonIgnore]
        public string CompletionMessage { get; set; }

        public DateTime? StateChangeTime
        {
            get
            {
                if (Started.HasValue)
                {
                    return Started.Value;
                }

                return Ended;
            }

            set
            {
            }
        }

        public bool SendUpdatesToClient
        {
            get
            {
                if (Body != null)
                {
                    return Body.SendUpdatesToClient;
                }

                return false;
            }

            set
            {
            }
        }

        public bool UpdateScheduledTask
        {
            get
            {
                if (Body != null)
                {
                    return Body.UpdateScheduledTask;
                }

                return false;
            }

            set
            {
            }
        }

        public DateTime? LastExecutionTime { get; set; }
    }

    public static class CommandResourceMapper
    {
        public static int ToSyntheticCommandId(this JobAttempt attempt)
        {
            return -attempt.Id;
        }

        public static CommandResource ToResource(this CommandModel model, JobAttempt attempt = null)
        {
            if (model == null)
            {
                return null;
            }

            return new CommandResource
            {
                Id = model.Id,

                Name = model.Name,
                CommandName = model.Name.SplitCamelCase(),
                Message = model.Message,
                Body = model.Body,
                Priority = model.Priority,
                Status = model.Status,
                Result = model.Result,
                Queued = model.QueuedAt,
                Started = model.StartedAt,
                Ended = model.EndedAt,
                Duration = model.Duration,
                Exception = model.Exception,
                Trigger = model.Trigger,
                JobId = attempt?.Id,
                JobState = attempt?.State,
                JobProgress = attempt?.Progress,
                JobAttemptCount = attempt?.AttemptCount,
                JobLastError = attempt?.LastError,

                ClientUserAgent = UserAgentParser.SimplifyUserAgent(model.Body.ClientUserAgent),

                CompletionMessage = model.Body.CompletionMessage,
                LastExecutionTime = model.Body.LastExecutionTime
            };
        }

        public static CommandResource ToResource(this JobAttempt attempt)
        {
            if (attempt?.CommandBody == null)
            {
                return null;
            }

            var status = MapStatus(attempt.State);
            var startedAt = status == CommandStatus.Queued ? null : attempt.StartedAt;
            var endedAt = status == CommandStatus.Completed || status == CommandStatus.Failed ? attempt.CompletedAt : null;
            TimeSpan? duration = startedAt.HasValue && endedAt.HasValue
                ? endedAt.Value - startedAt.Value
                : null;

            return new CommandResource
            {
                Id = attempt.ToSyntheticCommandId(),
                Name = attempt.CommandBody.Name,
                CommandName = attempt.CommandBody.Name.SplitCamelCase(),
                Message = GetMessage(attempt.State),
                Body = attempt.CommandBody,
                Priority = attempt.CommandPriority,
                Status = status,
                Result = MapResult(attempt.State),
                Queued = attempt.QueuedAt,
                Started = startedAt,
                Ended = endedAt,
                Duration = duration,
                Exception = status == CommandStatus.Failed ? attempt.LastError ?? "Canceled" : null,
                Trigger = attempt.CommandTrigger,
                JobId = attempt.Id,
                JobState = attempt.State,
                JobProgress = attempt.Progress,
                JobAttemptCount = attempt.AttemptCount,
                JobLastError = attempt.LastError,
                ClientUserAgent = UserAgentParser.SimplifyUserAgent(attempt.CommandBody.ClientUserAgent),
                CompletionMessage = attempt.CommandBody.CompletionMessage,
                LastExecutionTime = attempt.CommandBody.LastExecutionTime
            };
        }

        public static List<CommandResource> ToResource(this IEnumerable<CommandModel> models, IDictionary<int, JobAttempt> attemptsByCommandId = null)
        {
            attemptsByCommandId ??= new Dictionary<int, JobAttempt>();

            return models.Select(model =>
            {
                attemptsByCommandId.TryGetValue(model.Id, out var attempt);
                return model.ToResource(attempt);
            }).ToList();
        }

        private static string GetMessage(JobState state)
        {
            return state switch
            {
                JobState.Retrying => "Retrying",
                JobState.Canceled => "Canceled",
                _ => state.ToString()
            };
        }

        private static CommandStatus MapStatus(JobState state)
        {
            return state switch
            {
                JobState.Queued => CommandStatus.Queued,
                JobState.Retrying => CommandStatus.Queued,
                JobState.Running => CommandStatus.Started,
                JobState.Completed => CommandStatus.Completed,
                JobState.Failed => CommandStatus.Failed,
                JobState.Canceled => CommandStatus.Failed,
                _ => CommandStatus.Failed
            };
        }

        private static CommandResult MapResult(JobState state)
        {
            return state switch
            {
                JobState.Completed => CommandResult.Successful,
                JobState.Failed => CommandResult.Unsuccessful,
                JobState.Canceled => CommandResult.Unsuccessful,
                _ => CommandResult.Unknown
            };
        }
    }
}
