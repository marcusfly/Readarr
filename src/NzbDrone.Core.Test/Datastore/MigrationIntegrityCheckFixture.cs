using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class MigrationIntegrityCheckFixture : TestBase
    {
        private MigrationIntegrityCheck _subject;

        [SetUp]
        public void Setup()
        {
            _subject = new MigrationIntegrityCheck();
        }

        [Test]
        public void Check_should_throw_ArgumentNullException_when_db_is_null()
        {
            Action act = () => _subject.Check(null);

            act.Should().Throw<ArgumentNullException>();
        }

        [Test]
        public void RequiredColumns_should_be_non_empty()
        {
            // The static list must always contain at least the core tables.
            // This guards against accidental truncation.
            MigrationIntegrityCheck.RequiredColumns.Should().NotBeEmpty();
            MigrationIntegrityCheck.RequiredColumns.Should().HaveCountGreaterThan(10);
        }

        [Test]
        public void RequiredColumns_entries_should_have_non_empty_table_and_column()
        {
            foreach (var (table, column) in MigrationIntegrityCheck.RequiredColumns)
            {
                table.Should().NotBeNullOrWhiteSpace($"entry ({table}, {column}) must have a non-empty Table");
                column.Should().NotBeNullOrWhiteSpace($"entry ({table}, {column}) must have a non-empty Column");
            }
        }

        [Test]
        public void RequiredColumns_should_contain_core_author_book_entries()
        {
            MigrationIntegrityCheck.RequiredColumns.Should().Contain(("Authors", "Id"));
            MigrationIntegrityCheck.RequiredColumns.Should().Contain(("Books", "Id"));
            MigrationIntegrityCheck.RequiredColumns.Should().Contain(("Editions", "Id"));
            MigrationIntegrityCheck.RequiredColumns.Should().Contain(("BookFiles", "Id"));
            MigrationIntegrityCheck.RequiredColumns.Should().Contain(("Config", "Key"));
        }

        [Test]
        public void RequiredIndexes_should_be_non_empty_and_well_formed()
        {
            MigrationIntegrityCheck.RequiredIndexes.Should().NotBeEmpty();

            foreach (var (table, columns, unique) in MigrationIntegrityCheck.RequiredIndexes)
            {
                table.Should().NotBeNullOrWhiteSpace();
                columns.Should().NotBeNullOrEmpty($"index definition for {table} should name at least one column");
                columns.Should().OnlyContain(column => !string.IsNullOrWhiteSpace(column));
                _ = unique;
            }
        }

        [Test]
        public void RequiredIndexes_should_not_contain_duplicates()
        {
            var duplicates = MigrationIntegrityCheck.RequiredIndexes
                .GroupBy(index => $"{index.Table}|{string.Join(",", index.Columns)}|{index.Unique}", StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            duplicates.Should().BeEmpty();
        }

        [Test]
        public void RequiredIndexes_should_contain_core_uniqueness_and_lookup_invariants()
        {
            MigrationIntegrityCheck.RequiredIndexes.Should().ContainEquivalentOf(("Config", new[] { "Key" }, true));
            MigrationIntegrityCheck.RequiredIndexes.Should().ContainEquivalentOf(("BookFiles", new[] { "Path" }, true));
            MigrationIntegrityCheck.RequiredIndexes.Should().ContainEquivalentOf(("Books", new[] { "AuthorMetadataId", "ReleaseDate" }, false));
            MigrationIntegrityCheck.RequiredIndexes.Should().ContainEquivalentOf(("MagazineIssues", new[] { "MagazineId", "IssueYear", "IssueMonth", "IssueDay" }, true));
        }

        [Test]
        public void RequiredForeignKeys_should_be_non_empty_and_well_formed()
        {
            MigrationIntegrityCheck.RequiredForeignKeys.Should().NotBeEmpty();

            foreach (var (table, columns, referencedTable, referencedColumns, onDelete) in MigrationIntegrityCheck.RequiredForeignKeys)
            {
                table.Should().NotBeNullOrWhiteSpace();
                columns.Should().NotBeNullOrEmpty();
                columns.Should().OnlyContain(column => !string.IsNullOrWhiteSpace(column));
                referencedTable.Should().NotBeNullOrWhiteSpace();
                referencedColumns.Should().NotBeNullOrEmpty();
                referencedColumns.Should().OnlyContain(column => !string.IsNullOrWhiteSpace(column));
                onDelete.Should().NotBeNullOrWhiteSpace();
            }
        }

        [Test]
        public void RequiredForeignKeys_should_not_contain_duplicates()
        {
            var duplicates = MigrationIntegrityCheck.RequiredForeignKeys
                .GroupBy(foreignKey => $"{foreignKey.Table}|{string.Join(",", foreignKey.Columns)}|{foreignKey.ReferencedTable}|{string.Join(",", foreignKey.ReferencedColumns)}|{foreignKey.OnDelete}", StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToArray();

            duplicates.Should().BeEmpty();
        }

        [Test]
        public void RequiredForeignKeys_should_cover_canonical_relationships()
        {
            MigrationIntegrityCheck.RequiredForeignKeys.Should().ContainEquivalentOf(("SeriesBookLink", new[] { "SeriesId" }, "Series", new[] { "Id" }, "CASCADE"));
            MigrationIntegrityCheck.RequiredForeignKeys.Should().ContainEquivalentOf(("MagazineIssues", new[] { "MagazineId" }, "Magazines", new[] { "Id" }, "CASCADE"));
            MigrationIntegrityCheck.RequiredForeignKeys.Should().ContainEquivalentOf(("MagazineIssueFiles", new[] { "MagazineIssueId" }, "MagazineIssues", new[] { "Id" }, "CASCADE"));
        }
    }
}
