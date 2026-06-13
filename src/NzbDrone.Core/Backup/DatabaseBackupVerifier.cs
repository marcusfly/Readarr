using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using Dapper;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Backup
{
    /// <summary>
    /// Verifies that a Readarr SQLite backup file is readable and contains the
    /// expected core tables.  This is a quick sanity check intended for the restore
    /// path and for scheduled integrity monitoring — it does not run the full
    /// migration suite against the backup.
    /// </summary>
    public interface IDatabaseBackupVerifier
    {
        /// <summary>
        /// Opens the SQLite file at <paramref name="path"/>, confirms it can be
        /// read, and checks that every table in <see cref="DatabaseBackupVerifier.RequiredTables"/> is
        /// present.
        /// </summary>
        /// <param name="path">Absolute path to a .db backup file.</param>
        /// <returns>
        /// A <see cref="BackupVerificationResult"/> describing whether the backup
        /// is valid and listing any problems found.
        /// </returns>
        BackupVerificationResult VerifyBackup(string path);
    }

    public class BackupVerificationResult
    {
        /// <summary><c>true</c> if the backup appears valid and usable.</summary>
        public bool IsValid => Problems.Count == 0;

        /// <summary>Human-readable list of issues discovered during verification.</summary>
        public IReadOnlyList<string> Problems { get; }

        public BackupVerificationResult(IReadOnlyList<string> problems)
        {
            Problems = problems ?? throw new ArgumentNullException(nameof(problems));
        }
    }

    public class DatabaseBackupVerifier : IDatabaseBackupVerifier
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(DatabaseBackupVerifier));

        /// <summary>
        /// Minimum set of tables that must exist for a backup to be considered
        /// usable.  If a table is missing the backup is from an incompatible
        /// (too-old) schema or is corrupt.
        /// </summary>
        public static readonly IReadOnlyList<string> RequiredTables = new List<string>
        {
            "Authors",
            "AuthorMetadata",
            "Books",
            "Editions",
            "BookFiles",
            "Config",
            "RootFolders",
            "QualityProfiles",
            "MetadataProfiles",
            "NamingConfig",
            "History",
            "Blocklist",
            "DownloadClients",
            "Indexers",
            "Tags",
            "ScheduledTasks",
            "Users",
        };

        public BackupVerificationResult VerifyBackup(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var problems = new List<string>();

            if (!File.Exists(path))
            {
                problems.Add($"Backup file does not exist: {path}");
                return new BackupVerificationResult(problems);
            }

            try
            {
                var connectionString = $"Data Source={path};Version=3;Read Only=True;";

                using var conn = new SQLiteConnection(connectionString);
                conn.Open();

                // Quick integrity check.
                var integrityResult = conn.QueryFirst<string>("PRAGMA integrity_check;");
                if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"SQLite integrity_check returned: {integrityResult}");
                    return new BackupVerificationResult(problems);
                }

                // Collect actual table names.
                var actualTables = new HashSet<string>(
                    conn.Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var required in RequiredTables)
                {
                    if (!actualTables.Contains(required))
                    {
                        problems.Add($"Backup is missing required table: {required}");
                    }
                }

                if (problems.Count == 0)
                {
                    Logger.Debug("Backup verification passed for {0} — all required tables present.", Path.GetFileName(path));
                }
                else
                {
                    Logger.Warn("Backup verification found {0} problem(s) in {1}.", problems.Count, Path.GetFileName(path));
                }
            }
            catch (SQLiteException ex)
            {
                var msg = $"Could not open backup file '{Path.GetFileName(path)}': {ex.Message}";
                Logger.Error(ex, msg);
                problems.Add(msg);
            }
            catch (Exception ex)
            {
                var msg = $"Unexpected error verifying backup '{Path.GetFileName(path)}': {ex.Message}";
                Logger.Error(ex, msg);
                problems.Add(msg);
            }

            return new BackupVerificationResult(problems);
        }
    }
}
