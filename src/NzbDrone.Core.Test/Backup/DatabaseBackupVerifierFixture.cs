using System;
using System.Data.SQLite;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Backup;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Backup
{
    [TestFixture]
    public class DatabaseBackupVerifierFixture : TestBase
    {
        private DatabaseBackupVerifier _subject;
        private string _tempDir;

        [SetUp]
        public void Setup()
        {
            _subject = new DatabaseBackupVerifier();
            _tempDir = Path.Combine(Path.GetTempPath(), "readarr_backup_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        private string CreateSqliteDb(Action<SQLiteConnection> populate)
        {
            var path = Path.Combine(_tempDir, $"test_{Guid.NewGuid():N}.db");
            var connectionString = $"Data Source={path};Version=3;";

            using var conn = new SQLiteConnection(connectionString);
            conn.Open();

            populate(conn);

            return path;
        }

        private static void CreateAllRequiredTables(SQLiteConnection conn)
        {
            foreach (var table in DatabaseBackupVerifier.RequiredTables)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"CREATE TABLE IF NOT EXISTS \"{table}\" (Id INTEGER PRIMARY KEY)";
                cmd.ExecuteNonQuery();
            }
        }

        [Test]
        public void VerifyBackup_should_pass_for_valid_backup()
        {
            var path = CreateSqliteDb(CreateAllRequiredTables);

            var result = _subject.VerifyBackup(path);

            result.IsValid.Should().BeTrue("a backup with all required tables should be valid");
            result.Problems.Should().BeEmpty();
        }

        [Test]
        public void VerifyBackup_should_fail_when_file_does_not_exist()
        {
            var path = Path.Combine(_tempDir, "nonexistent.db");

            var result = _subject.VerifyBackup(path);

            result.IsValid.Should().BeFalse();
            result.Problems.Should().NotBeEmpty();
        }

        [Test]
        public void VerifyBackup_should_fail_when_required_table_is_missing()
        {
            var path = CreateSqliteDb(conn =>
            {
                // Only create some tables — deliberately omit Authors
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "CREATE TABLE Config (Id INTEGER PRIMARY KEY, Key TEXT, Value TEXT)";
                cmd.ExecuteNonQuery();
            });

            var result = _subject.VerifyBackup(path);

            result.IsValid.Should().BeFalse();
            result.Problems.Should().Contain(p => p.Contains("Authors"),
                "missing Authors table must be flagged");
        }

        [Test]
        public void VerifyBackup_should_fail_for_corrupt_file()
        {
            var path = Path.Combine(_tempDir, "corrupt.db");

            // Write garbage bytes that are not a valid SQLite file.
            File.WriteAllBytes(path, new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0xAB, 0xCD });

            var result = _subject.VerifyBackup(path);

            result.IsValid.Should().BeFalse();
            result.Problems.Should().NotBeEmpty();
        }

        [Test]
        public void VerifyBackup_should_throw_for_null_path()
        {
            Action act = () => _subject.VerifyBackup(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void RequiredTables_should_include_core_author_and_book_tables()
        {
            DatabaseBackupVerifier.RequiredTables.Should().Contain("Authors");
            DatabaseBackupVerifier.RequiredTables.Should().Contain("Books");
            DatabaseBackupVerifier.RequiredTables.Should().Contain("Editions");
            DatabaseBackupVerifier.RequiredTables.Should().Contain("BookFiles");
        }
    }
}
