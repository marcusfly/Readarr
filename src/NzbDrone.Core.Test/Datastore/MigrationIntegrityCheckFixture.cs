using System;
using System.Data;
using FluentAssertions;
using Moq;
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
    }
}
