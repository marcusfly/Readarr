using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using NLog;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Datastore
{
    /// <summary>
    /// Verifies on startup that the live database schema contains every table and
    /// column that the migration stack is expected to have produced.
    ///
    /// This is a structural (DDL-level) check only — it does not inspect row data.
    /// A mismatch means either a migration was not applied or the database was
    /// modified outside of the migration framework; either case should surface as a
    /// warning rather than a hard failure so the application can still start.
    /// </summary>
    public interface IMigrationIntegrityCheck
    {
        /// <summary>
        /// Runs all structural assertions against <paramref name="db"/>.
        /// Returns a list of human-readable problem descriptions; an empty list
        /// means the schema is intact.
        /// </summary>
        IReadOnlyList<string> Check(IDatabase db);
    }

    public class MigrationIntegrityCheck : IMigrationIntegrityCheck
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(MigrationIntegrityCheck));

        /// <summary>
        /// The canonical set of (table, column) pairs that must exist after all
        /// migrations have been applied.  Add entries here whenever a migration
        /// creates a new table or adds a column.
        /// </summary>
        public static readonly IReadOnlyList<(string Table, string Column)> RequiredColumns =
            new List<(string, string)>
            {
                // Config
                ("Config", "Id"),
                ("Config", "Key"),
                ("Config", "Value"),

                // RootFolders
                ("RootFolders", "Id"),
                ("RootFolders", "Path"),

                // Authors
                ("Authors", "Id"),
                ("Authors", "CleanName"),
                ("Authors", "Path"),
                ("Authors", "AudiobookPath"),
                ("Authors", "Monitored"),
                ("Authors", "AuthorMetadataId"),

                // AuthorMetadata
                ("AuthorMetadata", "Id"),
                ("AuthorMetadata", "ForeignAuthorId"),
                ("AuthorMetadata", "Name"),

                // Books
                ("Books", "Id"),
                ("Books", "AuthorMetadataId"),
                ("Books", "ForeignBookId"),
                ("Books", "Title"),
                ("Books", "Monitored"),

                // Magazines
                ("Magazines", "Id"),
                ("Magazines", "CleanTitle"),
                ("Magazines", "Title"),
                ("Magazines", "NormalizedTitle"),
                ("Magazines", "Aliases"),
                ("Magazines", "Issn"),
                ("Magazines", "IssnL"),
                ("Magazines", "WikidataId"),
                ("Magazines", "Publisher"),
                ("Magazines", "Country"),
                ("Magazines", "Language"),
                ("Magazines", "Monitored"),
                ("Magazines", "Path"),
                ("Magazines", "RootFolderPath"),
                ("Magazines", "QualityProfileId"),
                ("Magazines", "MetadataProfileId"),
                ("Magazines", "Tags"),
                ("Magazines", "Added"),
                ("Magazines", "LastInfoSync"),
                ("Magazines", "AddOptions"),

                // MagazineIssues
                ("MagazineIssues", "Id"),
                ("MagazineIssues", "MagazineId"),
                ("MagazineIssues", "IssueYear"),
                ("MagazineIssues", "IssueMonth"),
                ("MagazineIssues", "IssueDay"),
                ("MagazineIssues", "Volume"),
                ("MagazineIssues", "IssueNumber"),
                ("MagazineIssues", "ReleaseTitle"),
                ("MagazineIssues", "Monitored"),
                ("MagazineIssues", "Added"),
                ("MagazineIssues", "LastSearchTime"),

                // MagazineIssueFiles
                ("MagazineIssueFiles", "Id"),
                ("MagazineIssueFiles", "MagazineIssueId"),
                ("MagazineIssueFiles", "MagazineId"),
                ("MagazineIssueFiles", "Path"),
                ("MagazineIssueFiles", "Size"),
                ("MagazineIssueFiles", "DateAdded"),
                ("MagazineIssueFiles", "Quality"),
                ("MagazineIssueFiles", "MediaInfo"),

                // MagazineRootFolders
                ("MagazineRootFolders", "Id"),
                ("MagazineRootFolders", "Name"),
                ("MagazineRootFolders", "Path"),
                ("MagazineRootFolders", "DefaultQualityProfileId"),
                ("MagazineRootFolders", "DefaultMetadataProfileId"),
                ("MagazineRootFolders", "DefaultMonitorOption"),
                ("MagazineRootFolders", "DefaultTags"),

                // Editions
                ("Editions", "Id"),
                ("Editions", "BookId"),
                ("Editions", "ForeignEditionId"),
                ("Editions", "Title"),

                // BookFiles
                ("BookFiles", "Id"),
                ("BookFiles", "EditionId"),
                ("BookFiles", "Quality"),
                ("BookFiles", "Size"),
                ("BookFiles", "Path"),
                ("BookFiles", "IndexerFlags"),

                // Series / SeriesBookLink
                ("Series", "Id"),
                ("Series", "ForeignSeriesId"),
                ("SeriesBookLink", "Id"),
                ("SeriesBookLink", "SeriesId"),
                ("SeriesBookLink", "BookId"),

                // History
                ("History", "Id"),
                ("History", "AuthorId"),
                ("History", "BookId"),
                ("History", "EventType"),

                // Blocklist (was Blacklist)
                ("Blocklist", "Id"),
                ("Blocklist", "AuthorId"),
                ("Blocklist", "IndexerFlags"),

                // DownloadClients
                ("DownloadClients", "Id"),
                ("DownloadClients", "Name"),
                ("DownloadClients", "Implementation"),
                ("DownloadClients", "RemoveCompletedDownloads"),
                ("DownloadClients", "RemoveFailedDownloads"),

                // DownloadClientStatus
                ("DownloadClientStatus", "Id"),
                ("DownloadClientStatus", "ProviderId"),

                // DownloadHistory
                ("DownloadHistory", "Id"),
                ("DownloadHistory", "EventType"),
                ("DownloadHistory", "AuthorId"),
                ("DownloadHistory", "DownloadId"),

                // Indexers
                ("Indexers", "Id"),
                ("Indexers", "Name"),
                ("Indexers", "Tags"),
                ("Indexers", "DownloadClientId"),

                // IndexerStatus
                ("IndexerStatus", "Id"),
                ("IndexerStatus", "ProviderId"),

                // Tags
                ("Tags", "Id"),
                ("Tags", "Label"),

                // NamingConfig
                ("NamingConfig", "Id"),

                // QualityProfiles
                ("QualityProfiles", "Id"),
                ("QualityProfiles", "Name"),

                // MetadataProfiles
                ("MetadataProfiles", "Id"),
                ("MetadataProfiles", "Name"),

                // ScheduledTasks
                ("ScheduledTasks", "Id"),
                ("ScheduledTasks", "TypeName"),

                // Commands
                ("Commands", "Id"),
                ("Commands", "Status"),
                ("Commands", "Result"),

                // Users
                ("Users", "Id"),
                ("Users", "Identifier"),

                // CustomFormats
                ("CustomFormats", "Id"),
                ("CustomFormats", "Name"),
                ("CustomFormats", "Specifications"),

                // ImportLists
                ("ImportLists", "Id"),
                ("ImportLists", "Name"),
                ("ImportLists", "EnableAutomaticAdd"),

                // ImportListStatus
                ("ImportListStatus", "Id"),
                ("ImportListStatus", "ProviderId"),

                // ImportListExclusions
                ("ImportListExclusions", "Id"),
                ("ImportListExclusions", "ForeignId"),

                // ImportAttempts
                ("ImportAttempts", "Id"),
                ("ImportAttempts", "SourcePath"),
                ("ImportAttempts", "DestinationPath"),
                ("ImportAttempts", "SourceSize"),
                ("ImportAttempts", "Status"),
                ("ImportAttempts", "StartedAt"),
                ("ImportAttempts", "FinishedAt"),
                ("ImportAttempts", "IsDryRun"),
                ("ImportAttempts", "ErrorMessage"),

                // Notifications
                ("Notifications", "Id"),
                ("Notifications", "Name"),
                ("Notifications", "OnAuthorAdded"),

                // NotificationStatus
                ("NotificationStatus", "Id"),
                ("NotificationStatus", "ProviderId"),
            };

        public IReadOnlyList<string> Check(IDatabase db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }

            var problems = new List<string>();

            try
            {
                using var conn = db.OpenConnection();
                var actualColumns = GetActualColumns(conn, db.DatabaseType);

                foreach (var (table, column) in RequiredColumns)
                {
                    var key = $"{table.ToLowerInvariant()}.{column.ToLowerInvariant()}";
                    if (!actualColumns.Contains(key))
                    {
                        var msg = $"Missing column: {table}.{column}";
                        Logger.Warn(msg);
                        problems.Add(msg);
                    }
                }

                if (problems.Count == 0)
                {
                    Logger.Debug("MigrationIntegrityCheck passed — all expected tables and columns are present.");
                }
                else
                {
                    Logger.Warn("MigrationIntegrityCheck found {0} schema problem(s). " +
                                "The database may be from an older schema version or was modified outside of migrations.",
                                problems.Count);
                }
            }
            catch (Exception ex)
            {
                var msg = $"MigrationIntegrityCheck could not query the schema: {ex.Message}";
                Logger.Error(ex, msg);
                problems.Add(msg);
            }

            return problems;
        }

        private static HashSet<string> GetActualColumns(IDbConnection conn, DatabaseType dbType)
        {
            IEnumerable<(string table, string column)> rows;

            if (dbType == DatabaseType.PostgreSQL)
            {
                rows = conn.Query<(string table_name, string column_name)>(
                    "SELECT table_name, column_name " +
                    "FROM information_schema.columns " +
                    "WHERE table_schema = 'public'")
                    .Select(r => (r.table_name, r.column_name));
            }
            else
            {
                // SQLite: enumerate via PRAGMA table_info for each table.
                var tables = conn
                    .Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                    .ToList();

                var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var table in tables)
                {
                    var columns = conn.Query<SqliteColumnInfo>($"PRAGMA table_info(\"{table}\")");
                    foreach (var col in columns)
                    {
                        result.Add($"{table.ToLowerInvariant()}.{col.Name.ToLowerInvariant()}");
                    }
                }

                return result;
            }

            return new HashSet<string>(
                rows.Select(r => $"{r.table.ToLowerInvariant()}.{r.column.ToLowerInvariant()}"),
                StringComparer.OrdinalIgnoreCase);
        }

        // Used only for SQLite PRAGMA deserialization.
        private class SqliteColumnInfo
        {
            public int Cid { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public int NotNull { get; set; }
            public string DfltValue { get; set; }
            public int Pk { get; set; }
        }
    }
}
