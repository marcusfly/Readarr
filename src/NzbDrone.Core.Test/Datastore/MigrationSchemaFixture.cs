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
    }
}
