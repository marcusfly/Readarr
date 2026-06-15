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
        ImportAttempt Begin(string sourcePath, string destinationPath, long sourceSize, bool isDryRun);

        /// <summary>Transitions an attempt from Pending to InProgress.</summary>
        void MarkInProgress(ImportAttempt attempt);

        /// <summary>Transitions an attempt to Completed and records the finish timestamp.</summary>
        void MarkCompleted(ImportAttempt attempt);

        /// <summary>Transitions an attempt to Failed and records the error message.</summary>
        void MarkFailed(ImportAttempt attempt, string errorMessage);

        /// <summary>Transitions an attempt to RolledBack after crash recovery.</summary>
        void MarkRolledBack(ImportAttempt attempt, string errorMessage = null);

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
        private readonly IMediaFileService _mediaFileService;
        private readonly Logger _logger;

        public ImportAttemptService(IImportAttemptRepository repository,
                                    IDiskProvider diskProvider,
                                    IMediaFileService mediaFileService,
                                    Logger logger)
        {
            _repository = repository;
            _diskProvider = diskProvider;
            _mediaFileService = mediaFileService;
            _logger = logger;
        }

        public ImportAttempt Begin(string sourcePath, string destinationPath, long sourceSize, bool isDryRun)
        {
            var attempt = new ImportAttempt
            {
                SourcePath = sourcePath,
                DestinationPath = destinationPath,
                SourceSize = sourceSize,
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

        public void MarkRolledBack(ImportAttempt attempt, string errorMessage = null)
        {
            attempt.Status = ImportAttemptStatus.RolledBack;
            attempt.FinishedAt = DateTime.UtcNow;
            attempt.ErrorMessage = errorMessage;
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
                        _diskProvider.FileExists(attempt.DestinationPath) &&
                        DestinationMatchesAttempt(attempt) &&
                        DestinationIsTracked(attempt))
                    {
                        _logger.Info(
                            "Recovery: destination file exists for attempt {0} ({1}). Marking Completed.",
                            attempt.Id,
                            attempt.DestinationPath);
                        MarkCompleted(attempt);
                    }
                    else
                    {
                        var errorMessage = "Destination file missing or did not match the recorded source file size.";
                        _logger.Warn(
                            "Recovery: destination file missing or incomplete for attempt {0} ({1} -> {2}). Marking RolledBack.",
                            attempt.Id,
                            attempt.SourcePath,
                            attempt.DestinationPath);
                        MarkRolledBack(attempt, errorMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Recovery: failed to resolve orphaned import attempt {0}.", attempt.Id);
                }
            }
        }

        private bool DestinationMatchesAttempt(ImportAttempt attempt)
        {
            if (attempt.SourceSize <= 0)
            {
                return true;
            }

            var destinationSize = _diskProvider.GetFileSize(attempt.DestinationPath);
            if (destinationSize == attempt.SourceSize)
            {
                return true;
            }

            _logger.Warn(
                "Recovery: destination file size mismatch for attempt {0}. Expected {1} bytes but found {2} bytes at {3}.",
                attempt.Id,
                attempt.SourceSize,
                destinationSize,
                attempt.DestinationPath);

            return false;
        }

        private bool DestinationIsTracked(ImportAttempt attempt)
        {
            var bookFile = _mediaFileService.GetFileWithPath(attempt.DestinationPath);

            if (bookFile == null)
            {
                _logger.Warn(
                    "Recovery: destination file exists for attempt {0}, but no matching BookFile row was found for {1}.",
                    attempt.Id,
                    attempt.DestinationPath);

                return false;
            }

            if (attempt.SourceSize <= 0 || bookFile.Size == attempt.SourceSize)
            {
                return true;
            }

            _logger.Warn(
                "Recovery: BookFile row size mismatch for attempt {0}. Expected {1} bytes but found {2} bytes for {3}.",
                attempt.Id,
                attempt.SourceSize,
                bookFile.Size,
                attempt.DestinationPath);

            return false;
        }
    }
}
