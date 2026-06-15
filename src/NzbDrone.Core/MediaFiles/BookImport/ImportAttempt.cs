using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.MediaFiles.BookImport
{
    /// <summary>
    /// Durable state for a single file-import operation.  A row is written before
    /// any file-system mutation so that a crash leaves a recoverable record.
    /// </summary>
    public class ImportAttempt : ModelBase
    {
        /// <summary>Source path of the file being imported.</summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Destination path selected by the file-naming service.
        /// Null until the rename calculation is complete.
        /// </summary>
        public string DestinationPath { get; set; }

        /// <summary>
        /// Expected source file size in bytes when the attempt was created.
        /// Used during crash recovery to avoid accepting partial destination files.
        /// </summary>
        public long SourceSize { get; set; }

        /// <summary>Lifecycle state of this attempt.</summary>
        public ImportAttemptStatus Status { get; set; }

        /// <summary>UTC timestamp when the attempt record was first created.</summary>
        public DateTime StartedAt { get; set; }

        /// <summary>UTC timestamp of the last status change. Null until the attempt finishes.</summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// Whether this was a dry-run (plan-only, no file-system changes).
        /// Dry-run attempts are always recorded as Completed with no actual move/copy.
        /// </summary>
        public bool IsDryRun { get; set; }

        /// <summary>Human-readable error message when Status is Failed.</summary>
        public string ErrorMessage { get; set; }
    }

    public enum ImportAttemptStatus
    {
        /// <summary>Created but no file-system work has started yet.</summary>
        Pending = 0,

        /// <summary>File-system operation is currently in progress.</summary>
        InProgress = 1,

        /// <summary>File was successfully moved/copied to its destination.</summary>
        Completed = 2,

        /// <summary>The operation failed; see ErrorMessage for details.</summary>
        Failed = 3,

        /// <summary>The attempt was detected as orphaned on startup and rolled back.</summary>
        RolledBack = 4
    }
}
