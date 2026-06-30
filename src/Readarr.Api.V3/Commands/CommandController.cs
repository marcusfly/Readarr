using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Composition;
using NzbDrone.Common.Serializer;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.MediaCover.Commands;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ProgressMessaging;
using NzbDrone.Core.Update.Commands;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Http;
using Readarr.Http.REST;
using Readarr.Http.Validation;

namespace Readarr.Api.V3.Commands
{
    [V1ApiController]
    public class CommandController : RestControllerWithSignalR<CommandResource, CommandModel>, IHandle<CommandUpdatedEvent>, IHandle<DurableJobUpdatedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IDownloadMonitoringCommandSubmitter _downloadMonitoringCommandSubmitter;
        private readonly IImportListSyncCommandSubmitter _importListSyncCommandSubmitter;
        private readonly IDurableJobScheduler _durableJobScheduler;
        private readonly IJobAttemptService _jobAttemptService;
        private readonly IRefreshCommandSubmitter _refreshCommandSubmitter;
        private readonly KnownTypes _knownTypes;
        private readonly Debouncer _debouncer;
        private readonly Dictionary<int, CommandResource> _pendingUpdates;
        private readonly CommandPriorityComparer _commandPriorityComparer = new CommandPriorityComparer();
        private bool _pendingSync;

        public CommandController(IManageCommandQueue commandQueueManager,
                             IDownloadMonitoringCommandSubmitter downloadMonitoringCommandSubmitter,
                             IImportListSyncCommandSubmitter importListSyncCommandSubmitter,
                             IDurableJobScheduler durableJobScheduler,
                             IJobAttemptService jobAttemptService,
                             IRefreshCommandSubmitter refreshCommandSubmitter,
                             IBroadcastSignalRMessage signalRBroadcaster,
                             KnownTypes knownTypes)
            : base(signalRBroadcaster)
        {
            _commandQueueManager = commandQueueManager;
            _downloadMonitoringCommandSubmitter = downloadMonitoringCommandSubmitter;
            _importListSyncCommandSubmitter = importListSyncCommandSubmitter;
            _durableJobScheduler = durableJobScheduler;
            _jobAttemptService = jobAttemptService;
            _refreshCommandSubmitter = refreshCommandSubmitter;
            _knownTypes = knownTypes;

            _debouncer = new Debouncer(SendUpdates, TimeSpan.FromSeconds(0.1));
            _pendingUpdates = new Dictionary<int, CommandResource>();

            PostValidator.RuleFor(c => c.Name).NotBlank();
        }

        protected override CommandResource GetResourceById(int id)
        {
            if (id < 0)
            {
                return GetProjectedDurableCommand(-id);
            }

            return _commandQueueManager.Get(id).ToResource(_jobAttemptService.FindByCommandId(id));
        }

        [RestPostById]
        public ActionResult<CommandResource> StartCommand([FromBody] JsonElement body)
        {
            var bodyJson = body.GetRawText();
            var commandResource = STJson.Deserialize<CommandResource>(bodyJson);
            var commandType =
                _knownTypes.GetImplementations(typeof(Command))
                               .Single(c => c.Name.Replace("Command", "")
                                             .Equals(commandResource.Name, StringComparison.InvariantCultureIgnoreCase));

            var priority = commandType == typeof(ManualImportCommand)
                ? CommandPriority.High
                : CommandPriority.Normal;

            dynamic command = STJson.Deserialize(bodyJson, commandType);

            command.Trigger = CommandTrigger.Manual;
            command.SuppressMessages = !command.SendUpdatesToClient;
            command.SendUpdatesToClient = true;
            command.ClientUserAgent = Request.Headers["User-Agent"];

            var trackedCommand = TrySubmitDurableCommand((object)command, priority) ??
                                 _commandQueueManager.Push(command, priority, CommandTrigger.Manual);

            return Created(trackedCommand.Id);
        }

        [HttpGet]
        public List<CommandResource> GetStartedCommands()
        {
            var commands = _commandQueueManager.All();
            var attempts = _jobAttemptService.GetAll();
            var attemptsByCommandId = attempts
                .Where(a => a.CommandId.HasValue)
                .GroupBy(a => a.CommandId.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AttemptCount).ThenByDescending(a => a.Id).First());

            var durableOnlyCommands = attempts
                .Where(a => ShouldProjectDurableAttempt(a, commands))
                .Select(a => a.ToResource())
                .Where(r => r != null);

            return commands
                .ToResource(attemptsByCommandId)
                .Concat(durableOnlyCommands)
                .OrderBy(c => c.Status, _commandPriorityComparer)
                .ThenByDescending(c => c.Priority)
                .ToList();
        }

        [RestDeleteById]
        public void CancelCommand(int id)
        {
            if (id < 0)
            {
                var attempt = _jobAttemptService.GetById(-id);

                if (attempt == null || !CanCancelProjectedAttempt(attempt))
                {
                    throw new NzbDroneClientException(HttpStatusCode.Conflict, "Unable to cancel task");
                }

                _jobAttemptService.MarkCanceled(attempt);
                return;
            }

            _commandQueueManager.Cancel(id);
        }

        [NonAction]
        public void Handle(CommandUpdatedEvent message)
        {
            if (message.Command.Body.SendUpdatesToClient)
            {
                lock (_pendingUpdates)
                {
                    _pendingUpdates[message.Command.Id] = message.Command.ToResource();
                }

                _debouncer.Execute();
            }
        }

        [NonAction]
        public void Handle(DurableJobUpdatedEvent message)
        {
            if (message.JobAttempt?.CommandBody?.SendUpdatesToClient != true)
            {
                return;
            }

            lock (_pendingUpdates)
            {
                _pendingSync = true;
            }

            _debouncer.Execute();
        }

        private void SendUpdates()
        {
            lock (_pendingUpdates)
            {
                var pendingUpdates = _pendingUpdates.Values.ToArray();
                _pendingUpdates.Clear();
                var shouldSync = _pendingSync;
                _pendingSync = false;

                foreach (var pendingUpdate in pendingUpdates)
                {
                    BroadcastResourceChange(ModelAction.Updated, pendingUpdate);

                    if (pendingUpdate.Name == typeof(MessagingCleanupCommand).Name.Replace("Command", "") &&
                        pendingUpdate.Status == CommandStatus.Completed)
                    {
                        BroadcastResourceChange(ModelAction.Sync);
                    }
                }

                if (shouldSync)
                {
                    BroadcastResourceChange(ModelAction.Sync);
                }
            }
        }

        private CommandModel TrySubmitDurableCommand(object command, CommandPriority priority)
        {
            var attempt = command switch
            {
                RefreshAuthorCommand refreshAuthor => _refreshCommandSubmitter.Submit(refreshAuthor, priority, CommandTrigger.Manual),
                BulkRefreshAuthorCommand bulkRefreshAuthor => _refreshCommandSubmitter.Submit(bulkRefreshAuthor, priority, CommandTrigger.Manual),
                RefreshBookCommand refreshBook => _refreshCommandSubmitter.Submit(refreshBook, priority, CommandTrigger.Manual),
                BulkRefreshBookCommand bulkRefreshBook => _refreshCommandSubmitter.Submit(bulkRefreshBook, priority, CommandTrigger.Manual),
                ImportListSyncCommand importListSync => _importListSyncCommandSubmitter.Submit(importListSync, priority, CommandTrigger.Manual),
                RefreshMonitoredDownloadsCommand refreshMonitoredDownloads => _downloadMonitoringCommandSubmitter.Submit(refreshMonitoredDownloads, priority, CommandTrigger.Manual),
                ProcessMonitoredDownloadsCommand processMonitoredDownloads => _downloadMonitoringCommandSubmitter.Submit(processMonitoredDownloads, priority, CommandTrigger.Manual),
                BackupCommand backup => _durableJobScheduler.Submit(backup, "backup", priority, CommandTrigger.Manual),
                ApplicationUpdateCheckCommand applicationUpdateCheck => _durableJobScheduler.Submit(applicationUpdateCheck, $"application-update-check:{applicationUpdateCheck.InstallMajorUpdate}", priority, CommandTrigger.Manual),
                RssSyncCommand rssSync => _durableJobScheduler.Submit(rssSync, "rss-sync", priority, CommandTrigger.Manual),
                CheckHealthCommand checkHealth => _durableJobScheduler.Submit(checkHealth, "check-health", priority, CommandTrigger.Manual),
                HousekeepingCommand housekeeping => _durableJobScheduler.Submit(housekeeping, "housekeeping", priority, CommandTrigger.Manual),
                ProcessDeferredCoversCommand processDeferredCovers => _durableJobScheduler.Submit(processDeferredCovers, "process-deferred-covers", priority, CommandTrigger.Manual),
                MessagingCleanupCommand messagingCleanup => _durableJobScheduler.Submit(messagingCleanup, "messaging-cleanup", priority, CommandTrigger.Manual),
                RescanFoldersCommand rescanFolders when CanSubmitDurableFullRescan(rescanFolders) => _durableJobScheduler.Submit(rescanFolders, "rescan-folders:all", priority, CommandTrigger.Manual),
                _ => null
            };

            if (attempt?.CommandId == null)
            {
                return null;
            }

            return _commandQueueManager.Get(attempt.CommandId.Value);
        }

        private CommandResource GetProjectedDurableCommand(int jobAttemptId)
        {
            var attempt = _jobAttemptService.GetById(jobAttemptId);

            if (!ShouldProjectDurableAttempt(attempt, _commandQueueManager.All()))
            {
                return null;
            }

            return attempt.ToResource();
        }

        private static bool ShouldProjectDurableAttempt(JobAttempt attempt, List<CommandModel> liveCommands)
        {
            if (attempt?.CommandBody == null)
            {
                return false;
            }

            if (attempt.State != JobState.Queued &&
                attempt.State != JobState.Running &&
                attempt.State != JobState.Retrying)
            {
                return false;
            }

            if (attempt.CommandId.HasValue && liveCommands.Any(c => c.Id == attempt.CommandId.Value))
            {
                return false;
            }

            return !liveCommands.Any(c =>
                c.Name == attempt.CommandBody.Name &&
                CommandEqualityComparer.Instance.Equals(c.Body, attempt.CommandBody));
        }

        private static bool CanCancelProjectedAttempt(JobAttempt attempt)
        {
            return attempt.State == JobState.Queued || attempt.State == JobState.Retrying;
        }

        private static bool CanSubmitDurableFullRescan(RescanFoldersCommand command)
        {
            return (command.Folders == null || command.Folders.Count == 0) &&
                   (command.AuthorIds == null || command.AuthorIds.Count == 0) &&
                   command.Filter == FilterFilesType.Known &&
                   command.AddNewAuthors;
        }
    }
}
