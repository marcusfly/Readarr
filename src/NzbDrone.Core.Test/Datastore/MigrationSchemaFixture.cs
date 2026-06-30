using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    /// <summary>
    /// Verifies that applying all migrations from a fresh database produces a
    /// schema that contains every table and column listed in
    /// <see cref="MigrationIntegrityCheck.RequiredColumns"/>.
    ///
    /// This test acts as a regression guard: if a future migration drops a
    /// required table or column, or if the RequiredColumns list falls out of sync
    /// with the migration history, this test will fail and flag the discrepancy.
    /// </summary>
    [TestFixture]
    [Category("DbMigrationTest")]
    [Category("DbTest")]
    public class MigrationSchemaFixture : DbTest
    {
        [Test]
        public void full_migration_should_produce_all_required_tables_and_columns()
        {
            // The base class SetupDb() already runs all migrations on a fresh DB.
            // We just need to verify the resulting schema.
            var check = new MigrationIntegrityCheck();
            var problems = check.Check(Mocker.Resolve<IDatabase>());

            problems.Should().BeEmpty(
                "all migrations applied to a fresh database must produce the canonical schema; " +
                "missing entries: {0}",
                string.Join(", ", problems));
        }

        [Test]
        public void full_migration_should_produce_core_tables()
        {
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var tables = conn
                .Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .ToHashSet();

            var coreExpected = new[]
            {
                "Authors",
                "AuthorMetadata",
                "Books",
                "Editions",
                "BookFiles",
                "Magazines",
                "MagazineIssues",
                "MagazineIssueFiles",
                "MagazineRootFolders",
                "Config",
                "RootFolders",
                "QualityProfiles",
                "MetadataProfiles",
                "History",
                "DownloadClients",
                "Indexers",
                "Tags",
                "ScheduledTasks",
                "Users",
                "NamingConfig",
                "Blocklist",
                "ImportAttempts",
            };

            foreach (var table in coreExpected)
            {
                tables.Should().Contain(table, $"table '{table}' must exist after all migrations");
            }
        }

        [Test]
        public void full_migration_should_create_magazine_tables()
        {
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var magazinesColumns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"Magazines\")")
                .Select(c => c.Name)
                .ToHashSet();

            magazinesColumns.Should().Contain(new[]
            {
                "Id",
                "CleanTitle",
                "Title",
                "NormalizedTitle",
                "Aliases",
                "Issn",
                "WikidataId",
                "Publisher",
                "Monitored",
                "Path",
                "RootFolderPath",
                "QualityProfileId",
                "MetadataProfileId",
                "Tags",
                "Added",
                "LastInfoSync",
                "AddOptions",
            });

            var magazineIssuesColumns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"MagazineIssues\")")
                .Select(c => c.Name)
                .ToHashSet();

            magazineIssuesColumns.Should().Contain(new[]
            {
                "Id",
                "MagazineId",
                "IssueYear",
                "IssueMonth",
                "IssueDay",
                "Volume",
                "IssueNumber",
                "ReleaseTitle",
                "Monitored",
                "Added",
                "LastSearchTime",
            });

            var magazineIssueFilesColumns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"MagazineIssueFiles\")")
                .Select(c => c.Name)
                .ToHashSet();

            magazineIssueFilesColumns.Should().Contain(new[]
            {
                "Id",
                "MagazineIssueId",
                "MagazineId",
                "Path",
                "Size",
                "DateAdded",
                "Quality",
                "MediaInfo",
            });

            var magazineRootFoldersColumns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"MagazineRootFolders\")")
                .Select(c => c.Name)
                .ToHashSet();

            magazineRootFoldersColumns.Should().Contain(new[]
            {
                "Id",
                "Name",
                "Path",
                "DefaultQualityProfileId",
                "DefaultMetadataProfileId",
                "DefaultMonitorOption",
                "DefaultTags",
            });
        }

        [Test]
        public void full_migration_should_create_ImportAttempts_columns()
        {
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var columns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"ImportAttempts\")")
                .Select(c => c.Name)
                .ToHashSet();

            columns.Should().Contain(new[]
            {
                "Id",
                "SourcePath",
                "DestinationPath",
                "SourceSize",
                "Status",
                "StartedAt",
                "FinishedAt",
                "IsDryRun",
                "ErrorMessage",
            });
        }

        [Test]
        public void full_migration_should_add_AudiobookPath_column_to_Authors()
        {
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var columns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"Authors\")")
                .Select(c => c.Name)
                .ToHashSet();

            columns.Should().Contain("AudiobookPath",
                "migration 044 must add AudiobookPath to Authors");
        }

        [Test]
        public void full_migration_should_add_IndexerFlags_column_to_BookFiles()
        {
            // Regression: migration 040 added IndexerFlags.
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var columns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"BookFiles\")")
                .Select(c => c.Name)
                .ToHashSet();

            columns.Should().Contain("IndexerFlags",
                "migration 040 must add IndexerFlags to BookFiles");
        }

        [Test]
        public void full_migration_should_add_RemoveCompletedDownloads_column_to_DownloadClients()
        {
            // Regression: migration 034/158 added per-client CDH settings.
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var columns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"DownloadClients\")")
                .Select(c => c.Name)
                .ToHashSet();

            columns.Should().Contain("RemoveCompletedDownloads",
                "migration 158 must add RemoveCompletedDownloads to DownloadClients");
            columns.Should().Contain("RemoveFailedDownloads",
                "migration 158 must add RemoveFailedDownloads to DownloadClients");
        }

        [Test]
        public void full_migration_should_add_Tags_column_to_Indexers()
        {
            // Regression: migration 028 added Tags to Indexers.
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();

            var columns = conn
                .Query<SqliteColumnInfo>("PRAGMA table_info(\"Indexers\")")
                .Select(c => c.Name)
                .ToHashSet();

            columns.Should().Contain("Tags",
                "migration 028 must add Tags to Indexers");
        }

        [Test]
        public void full_migration_should_create_required_indexes()
        {
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();
            var dbType = Mocker.Resolve<IDatabase>().DatabaseType;
            var actualIndexes = GetIndexes(conn, dbType);

            foreach (var (table, columns, unique) in MigrationIntegrityCheck.RequiredIndexes)
            {
                actualIndexes.Should().Contain(index =>
                        table == index.Table &&
                        (!unique || index.Unique) &&
                        index.Columns.SequenceEqual(columns),
                    $"the canonical migrated schema should contain {(unique ? "a unique " : "an ")}index on {table}({string.Join(", ", columns)})");
            }
        }

        [Test]
        public void full_migration_should_create_required_foreign_keys()
        {
            using var conn = Mocker.Resolve<IDatabase>().OpenConnection();
            var dbType = Mocker.Resolve<IDatabase>().DatabaseType;
            var actualForeignKeys = GetForeignKeys(conn, dbType);

            foreach (var (table, columns, referencedTable, referencedColumns, onDelete) in MigrationIntegrityCheck.RequiredForeignKeys)
            {
                actualForeignKeys.Should().Contain(foreignKey =>
                        table == foreignKey.Table &&
                        referencedTable == foreignKey.ReferencedTable &&
                        onDelete == foreignKey.OnDelete &&
                        foreignKey.Columns.SequenceEqual(columns) &&
                        foreignKey.ReferencedColumns.SequenceEqual(referencedColumns),
                    $"the canonical migrated schema should contain foreign key {table}({string.Join(", ", columns)}) -> {referencedTable}({string.Join(", ", referencedColumns)}) ON DELETE {onDelete}");
            }
        }

        // Shared DTO for PRAGMA table_info results.
        private class SqliteColumnInfo
        {
            public int Cid { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
            public int NotNull { get; set; }
            public string DfltValue { get; set; }
            public int Pk { get; set; }
        }

        private static ActualIndex[] GetIndexes(System.Data.IDbConnection conn, DatabaseType dbType)
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
                    .Select(row => new ActualIndex(row.TableName, row.Columns ?? new string[0], row.IsUnique))
                    .ToArray();
            }

            var tables = conn
                .Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .ToArray();

            return tables
                .SelectMany(table => conn.Query<SqliteIndexInfo>($"PRAGMA index_list(\"{table}\")")
                    .Select(index => new ActualIndex(
                        table,
                        conn.Query<SqliteIndexColumnInfo>($"PRAGMA index_info(\"{index.Name}\")")
                            .OrderBy(column => column.SeqNo)
                            .Select(column => column.Name)
                            .ToArray(),
                        index.Unique != 0)))
                .ToArray();
        }

        private static ActualForeignKey[] GetForeignKeys(System.Data.IDbConnection conn, DatabaseType dbType)
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
                    .ToArray();
            }

            var tables = conn
                .Query<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .ToArray();

            return tables
                .SelectMany(table => conn.Query($"PRAGMA foreign_key_list(\"{table}\")")
                    .Select(row => SqliteForeignKeyInfo.FromRow(row))
                    .GroupBy(foreignKey => new { foreignKey.Id, foreignKey.Table, foreignKey.OnDelete })
                    .Select(group => new ActualForeignKey(
                        table,
                        group.OrderBy(x => x.Seq).Select(x => x.From).Cast<string>().ToArray(),
                        group.Key.Table,
                        group.OrderBy(x => x.Seq).Select(x => x.To).Cast<string>().ToArray(),
                        group.Key.OnDelete)))
                .ToArray();
        }

        private class SqliteIndexInfo
        {
            public int Seq { get; set; }
            public string Name { get; set; }
            public int Unique { get; set; }
        }

        private class SqliteIndexColumnInfo
        {
            public int SeqNo { get; set; }
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
