using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaFiles.BookImport
{
    public interface IImportAttemptService
    {
        /// <summary>
        /// Creates an attempt record in the Pending state before any file-system work begins.
        /// </summary>
        ImportAttempt Begin(string sourcePath, string destinationPath, bool isDryRun);

        /// <summary>Transitions an attempt from Pending to InProgress.</summary>
        void MarkInProgress(ImportAttempt attempt);

        /// <summary>Transitions an attempt to Completed and records the finish timestamp.</summary>
        void MarkCompleted(ImportAttempt attempt);

        /// <summary>Transitions an attempt to Failed and records the error message.</summary>
        void MarkFailed(ImportAttempt attempt, string errorMessage);

        /// <summary>Transitions an attempt to RolledBack after crash recovery.</summary>
        void MarkRolledBack(ImportAttempt attempt);

        /// <summary>
        /// Returns all InProgress attempts from prior runs (for crash recovery on startup).
        /// </summary>
        List<ImportAttempt> GetOrphanedInProgressAttempts();
    }

    /// <summary>
    /// Manages durable ImportAttempt records and runs a crash-recovery scan at startup.
    /// </summary>
    public class ImportAttemptService : IImportAttemptService, IHandle<ApplicationStartedEvent>
    {
        private readonly IImportAttemptRepository _repository;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public ImportAttemptService(IImportAttemptRepository repository,
                                    IDiskProvider diskProvider,
                                    Logger logger)
        {
            _repository = repository;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public ImportAttempt Begin(string sourcePath, string destinationPath, bool isDryRun)
        {
            var attempt = new ImportAttempt
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                Status = ImportAttemptStatus.Pending,
                StartedAt = DateTime.UtcNow,
                IsDryRun = isDryRun
            };

            _repository.Insert(attempt);
            return attempt;
        }

        public void MarkInProgress(ImportAttempt attempt)
        {
            attempt.Status = ImportAttemptStatus.InProgress;
            _repository.Update(attempt);
        }

        public void MarkCompleted(ImportAttempt attempt)
        {
            attempt.Status = ImportAttemptStatus.Completed;
            attempt.FinishedAt = DateTime.UtcNow;
            _repository.Update(attempt);
        }

        public void MarkFailed(ImportAttempt attempt, string errorMessage)
        {
            attempt.Status = ImportAttemptStatus.Failed;
            attempt.FinishedAt = DateTime.UtcNow;
            attempt.ErrorMessage = errorMessage;
            _repository.Update(attempt);
        }

        public void MarkRolledBack(ImportAttempt attempt)
        {
            attempt.Status = ImportAttemptStatus.RolledBack;
            attempt.FinishedAt = DateTime.UtcNow;
            _repository.Update(attempt);
        }

        public List<ImportAttempt> GetOrphanedInProgressAttempts()
        {
            return _repository.FindInProgress();
        }

        // -----------------------------------------------------------------------
        // Startup crash-recovery scan
        // -----------------------------------------------------------------------

        /// <summary>
        /// On every application start, look for ImportAttempt rows that were left
        /// InProgress by a prior (crashed) process and attempt to resolve them.
        ///
        /// Recovery heuristic:
        ///   - If the destination file already exists the copy/move succeeded before the
        ///     crash: mark the attempt Completed.
        ///   - Otherwise, the partial operation could not have created the destination:
        ///     mark the attempt RolledBack so an operator knows to retry the import.
        /// </summary>
        public void Handle(ApplicationStartedEvent message)
        {
            var orphans = _repository.FindInProgress();

            if (orphans.Count == 0)
            {
                return;
            }

            _logger.Warn("Found {0} InProgress import attempt(s) from a previous run. Running crash-recovery scan.", orphans.Count);

            foreach (var attempt in orphans)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(attempt.DestinationPath) &&
                        _diskProvider.FileExists(attempt.DestinationPath))
                    {
                        _logger.Info(
                            "Recovery: destination file exists for attempt {0} ({1}). Marking Completed.",
                            attempt.Id,
                            attempt.DestinationPath);
                        MarkCompleted(attempt);
                    }
                    else
                    {
                        _logger.Warn(
                            "Recovery: destination file missing for attempt {0} ({1} → {2}). Marking RolledBack.",
                            attempt.Id,
                            attempt.SourcePath,
                            attempt.DestinationPath);
                        MarkRolledBack(attempt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Recovery: failed to resolve orphaned import attempt {0}.", attempt.Id);
                }
            }
        }
    }
}
