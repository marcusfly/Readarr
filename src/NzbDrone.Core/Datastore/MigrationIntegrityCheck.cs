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

        /// <summary>
        /// Canonical indexes and unique constraints that must exist after all
        /// migrations have been applied. Column order matters for composite
        /// indexes because query behavior and uniqueness semantics depend on it.
        /// </summary>
        public static readonly IReadOnlyList<(string Table, string[] Columns, bool Unique)> RequiredIndexes =
            new List<(string, string[], bool)>
            {
                ("Config", new[] { "Key" }, true),
                ("RootFolders", new[] { "Path" }, true),
                ("Authors", new[] { "AuthorMetadataId" }, true),
                ("Authors", new[] { "CleanName" }, false),
                ("Authors", new[] { "Path" }, false),
                ("Authors", new[] { "Monitored" }, false),
                ("AuthorMetadata", new[] { "ForeignAuthorId" }, true),
                ("Books", new[] { "AuthorMetadataId" }, false),
                ("Books", new[] { "AuthorMetadataId", "ReleaseDate" }, false),
                ("Series", new[] { "ForeignSeriesId" }, true),
                ("Editions", new[] { "ForeignEditionId" }, true),
                ("Editions", new[] { "BookId" }, false),
                ("BookFiles", new[] { "Path" }, true),
                ("BookFiles", new[] { "EditionId" }, false),
                ("History", new[] { "BookId", "Date" }, false),
                ("History", new[] { "DownloadId", "Date" }, false),
                ("ImportAttempts", new[] { "Status" }, false),
                ("ImportAttempts", new[] { "SourcePath" }, false),
                ("Users", new[] { "Identifier" }, true),
                ("Magazines", new[] { "CleanTitle" }, false),
                ("Magazines", new[] { "Path" }, false),
                ("MagazineIssues", new[] { "MagazineId" }, false),
                ("MagazineIssues", new[] { "MagazineId", "IssueYear", "IssueMonth", "IssueDay" }, true),
                ("MagazineIssueFiles", new[] { "MagazineIssueId" }, false),
                ("MagazineIssueFiles", new[] { "MagazineId" }, false),
                ("MagazineIssueFiles", new[] { "Path" }, true),
                ("MagazineRootFolders", new[] { "Path" }, true),
                ("Tags", new[] { "Label" }, true),
            };

        /// <summary>
        /// Foreign-key relationships that define canonical post-migration table
        /// integrity for tables that currently enforce relational links.
        /// </summary>
        public static readonly IReadOnlyList<(string Table, string[] Columns, string ReferencedTable, string[] ReferencedColumns, string OnDelete)> RequiredForeignKeys =
            new List<(string, string[], string, string[], string)>
            {
                ("SeriesBookLink", new[] { "SeriesId" }, "Series", new[] { "Id" }, "CASCADE"),
                ("SeriesBookLink", new[] { "BookId" }, "Books", new[] { "Id" }, "CASCADE"),
                ("MagazineIssues", new[] { "MagazineId" }, "Magazines", new[] { "Id" }, "CASCADE"),
                ("MagazineIssueFiles", new[] { "MagazineIssueId" }, "MagazineIssues", new[] { "Id" }, "CASCADE"),
                ("MagazineIssueFiles", new[] { "MagazineId" }, "Magazines", new[] { "Id" }, "CASCADE"),
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

                var actualIndexes = GetActualIndexes(conn, db.DatabaseType);
                foreach (var (table, columns, unique) in RequiredIndexes)
                {
                    if (!HasMatchingIndex(actualIndexes, table, columns, unique))
                    {
                        var msg = $"Missing {(unique ? "unique " : string.Empty)}index: {table}({string.Join(", ", columns)})";
                        Logger.Warn(msg);
                        problems.Add(msg);
                    }
                }

                var actualForeignKeys = GetActualForeignKeys(conn, db.DatabaseType);
                foreach (var (table, columns, referencedTable, referencedColumns, onDelete) in RequiredForeignKeys)
                {
                    if (!HasMatchingForeignKey(actualForeignKeys, table, columns, referencedTable, referencedColumns, onDelete))
                    {
                        var msg = $"Missing foreign key: {table}({string.Join(", ", columns)}) -> {referencedTable}({string.Join(", ", referencedColumns)}) ON DELETE {onDelete}";
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

        private static List<ActualIndex> GetActualIndexes(IDbConnection conn, DatabaseType dbType)
        {
            if (dbType == DatabaseType.PostgreSQL)
            {
                return conn.Query<PostgresIndexRow>(
                        @"SELECT t.relname AS TableName,
                                 ix.indisunique AS IsUnique,
                                 ARRAY_AGG(a.attname ORDER BY key_columns.ordinality) AS Columns
                          FROM pg_class t
                          INNER JOIN pg_namespace ns ON ns.oid = t.relnamespace
                          INNER JOIN pg_index ix ON ix.indrelid = t.oid
                          INNER JOIN UNNEST(ix.indkey) WITH ORDINALITY AS key_columns(attnum, ordinality) ON TRUE
                          INNER JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = key_columns.attnum
                          WHERE ns.nspname = 'public'
                          GROUP BY t.relname, ix.indexrelid, ix.indisunique")
                        .Select(row => new ActualIndex(row.TableName, row.Columns ?? Array.Empty<string>(), row.IsUnique))
                        .ToList();
            }

            var tables = conn
                .Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .ToList();

            var indexes = new List<ActualIndex>();

            foreach (var table in tables)
            {
                var tableIndexes = conn.Query<SqliteIndexInfo>($"PRAGMA index_list(\"{table}\")");
                foreach (var index in tableIndexes)
                {
                    var columns = conn.Query<SqliteIndexColumnInfo>($"PRAGMA index_info(\"{index.Name}\")")
                        .OrderBy(c => c.SeqNo)
                        .Select(c => c.Name)
                        .ToArray();

                    indexes.Add(new ActualIndex(table, columns, index.Unique != 0));
                }
            }

            return indexes;
        }

        private static List<ActualForeignKey> GetActualForeignKeys(IDbConnection conn, DatabaseType dbType)
        {
            if (dbType == DatabaseType.PostgreSQL)
            {
                return conn.Query<PostgresForeignKeyRow>(
                        @"SELECT tc.table_name AS TableName,
                                 kcu.column_name AS ColumnName,
                                 ccu.table_name AS ReferencedTableName,
                                 ccu.column_name AS ReferencedColumnName,
                                 rc.delete_rule AS DeleteRule,
                                 tc.constraint_name AS ConstraintName,
                                 kcu.ordinal_position AS OrdinalPosition
                          FROM information_schema.table_constraints tc
                          INNER JOIN information_schema.key_column_usage kcu
                              ON tc.constraint_name = kcu.constraint_name
                             AND tc.table_schema = kcu.table_schema
                          INNER JOIN information_schema.constraint_column_usage ccu
                              ON ccu.constraint_name = tc.constraint_name
                             AND ccu.table_schema = tc.table_schema
                          INNER JOIN information_schema.referential_constraints rc
                              ON rc.constraint_name = tc.constraint_name
                             AND rc.constraint_schema = tc.table_schema
                          WHERE tc.constraint_type = 'FOREIGN KEY'
                            AND tc.table_schema = 'public'")
                        .GroupBy(row => new { row.TableName, row.ConstraintName, row.ReferencedTableName, row.DeleteRule })
                        .Select(group => new ActualForeignKey(
                            group.Key.TableName,
                            group.OrderBy(x => x.OrdinalPosition).Select(x => x.ColumnName).ToArray(),
                            group.Key.ReferencedTableName,
                            group.OrderBy(x => x.OrdinalPosition).Select(x => x.ReferencedColumnName).ToArray(),
                            group.Key.DeleteRule))
                        .ToList();
            }

            var tables = conn
                .Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .ToList();

            var foreignKeys = new List<ActualForeignKey>();

            foreach (var table in tables)
            {
                var tableForeignKeys = conn.Query($"PRAGMA foreign_key_list(\"{table}\")")
                    .Select(SqliteForeignKeyInfo.FromRow)
                    .GroupBy(fk => new { fk.Id, fk.Table, fk.OnDelete })
                    .Select(group => new ActualForeignKey(
                        table,
                        group.OrderBy(x => x.Seq).Select(x => x.From).Cast<string>().ToArray(),
                        group.Key.Table,
                        group.OrderBy(x => x.Seq).Select(x => x.To).Cast<string>().ToArray(),
                        group.Key.OnDelete));

                foreignKeys.AddRange(tableForeignKeys);
            }

            return foreignKeys;
        }

        private static bool HasMatchingIndex(IEnumerable<ActualIndex> actualIndexes, string table, IReadOnlyList<string> columns, bool unique)
        {
            return actualIndexes.Any(index =>
                string.Equals(index.Table, table, StringComparison.OrdinalIgnoreCase) &&
                (!unique || index.Unique) &&
                ColumnsMatch(index.Columns, columns));
        }

        private static bool HasMatchingForeignKey(IEnumerable<ActualForeignKey> actualForeignKeys, string table, IReadOnlyList<string> columns, string referencedTable, IReadOnlyList<string> referencedColumns, string onDelete)
        {
            return actualForeignKeys.Any(foreignKey =>
                string.Equals(foreignKey.Table, table, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(foreignKey.ReferencedTable, referencedTable, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(foreignKey.OnDelete, onDelete, StringComparison.OrdinalIgnoreCase) &&
                ColumnsMatch(foreignKey.Columns, columns) &&
                ColumnsMatch(foreignKey.ReferencedColumns, referencedColumns));
        }

        private static bool ColumnsMatch(IReadOnlyList<string> actual, IReadOnlyList<string> expected)
        {
            if (actual.Count != expected.Count)
            {
                return false;
            }

            for (var i = 0; i < expected.Count; i++)
            {
                if (!string.Equals(actual[i], expected[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
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

        private class SqliteIndexInfo
        {
            public int Seq { get; set; }
            public string Name { get; set; }
            public int Unique { get; set; }
            public string Origin { get; set; }
            public int Partial { get; set; }
        }

        private class SqliteIndexColumnInfo
        {
            public int SeqNo { get; set; }
            public int Cid { get; set; }
            public string Name { get; set; }
        }

        private class SqliteForeignKeyInfo
        {
            public int Id { get; set; }
            public int Seq { get; set; }
            public string Table { get; set; }
            public string From { get; set; }
            public string To { get; set; }
            public string OnDelete { get; set; }

            public static SqliteForeignKeyInfo FromRow(dynamic row)
            {
                var dictionary = (System.Collections.Generic.IDictionary<string, object>)row;

                return new SqliteForeignKeyInfo
                {
                    Id = System.Convert.ToInt32(dictionary["id"]),
                    Seq = System.Convert.ToInt32(dictionary["seq"]),
                    Table = dictionary["table"]?.ToString(),
                    From = dictionary["from"]?.ToString(),
                    To = dictionary["to"]?.ToString(),
                    OnDelete = dictionary["on_delete"]?.ToString()
                };
            }
        }

        private class PostgresIndexRow
        {
            public string TableName { get; set; }
            public bool IsUnique { get; set; }
            public string[] Columns { get; set; }
        }

        private class PostgresForeignKeyRow
        {
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string ReferencedTableName { get; set; }
            public string ReferencedColumnName { get; set; }
            public string DeleteRule { get; set; }
            public string ConstraintName { get; set; }
            public int OrdinalPosition { get; set; }
        }

        private sealed class ActualIndex
        {
            public ActualIndex(string table, string[] columns, bool unique)
            {
                Table = table;
                Columns = columns;
                Unique = unique;
            }

            public string Table { get; }
            public string[] Columns { get; }
            public bool Unique { get; }
        }

        private sealed class ActualForeignKey
        {
            public ActualForeignKey(string table, string[] columns, string referencedTable, string[] referencedColumns, string onDelete)
            {
                Table = table;
                Columns = columns;
                ReferencedTable = referencedTable;
                ReferencedColumns = referencedColumns;
                OnDelete = onDelete;
            }

            public string Table { get; }
            public string[] Columns { get; }
            public string ReferencedTable { get; }
            public string[] ReferencedColumns { get; }
            public string OnDelete { get; }
        }
    }
}
