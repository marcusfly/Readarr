using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Qualities;
using NzbDrone.Integration.Test.Client;
using Readarr.Api.V3.Author;
using Readarr.Api.V3.Books;

namespace NzbDrone.Integration.Test.ApiTests.WorkflowTests
{
    /// <summary>
    /// End-to-end workflow tests covering the critical happy-path for lookup,
    /// adding an author, refreshing books, importing a file, and verifying rename
    /// previews against the live integration harness.
    ///
    /// Run with:
    ///   dotnet test --filter "FullyQualifiedName~CriticalWorkflowFixture"
    /// </summary>
    [TestFixture]
    [Ignore("Integration workflow tests still need live-provider validation before CI enablement", Until = "2099-01-01 00:00:00Z")]
    public class CriticalWorkflowFixture : IntegrationTest
    {
        private const string ExpectedAuthorName = "J.K. Rowling";

        private int _addedAuthorId;

        [SetUp]
        public void WorkflowSetUp()
        {
            _addedAuthorId = 0;

            foreach (var existing in Author.All().Where(a => string.Equals(a.AuthorName, ExpectedAuthorName, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                Author.Delete(existing.Id);
            }

            Commands.WaitAll();
        }

        [TearDown]
        public void WorkflowTearDown()
        {
            if (_addedAuthorId == 0)
            {
                return;
            }

            try
            {
                Author.Delete(_addedAuthorId);
                Commands.WaitAll();
            }
            catch
            {
                // Best-effort cleanup; the harness tears the process down after the fixture.
            }
        }

        private AuthorResource AddExpectedAuthor(bool monitored = true)
        {
            var results = Author.Lookup(ExpectedAuthorName);
            results.Should().NotBeNullOrEmpty($"lookup for '{ExpectedAuthorName}' must return at least one result");

            var authorPayload = results.FirstOrDefault(a => string.Equals(a.AuthorName, ExpectedAuthorName, StringComparison.OrdinalIgnoreCase));
            authorPayload.Should().NotBeNull($"lookup for '{ExpectedAuthorName}' must include the expected author");

            authorPayload.QualityProfileId = 1;
            authorPayload.MetadataProfileId = 1;
            authorPayload.Path = Path.Combine(AuthorRootFolder, authorPayload.AuthorName);
            authorPayload.Monitored = monitored;
            authorPayload.AddOptions = new NzbDrone.Core.Books.AddAuthorOptions();

            Directory.CreateDirectory(authorPayload.Path);

            var added = Author.Post(authorPayload);
            _addedAuthorId = added.Id;

            Commands.WaitAll();

            return added;
        }

        private BookResource GetFirstBook(int authorId)
        {
            WaitForCompletion(() => Books.GetBooksInAuthor(authorId).Any(), timeout: 30000, interval: 1000);
            return Books.GetBooksInAuthor(authorId).First();
        }

        [Test]
        [Order(1)]
        public void search_author_by_name_returns_results()
        {
            var results = Author.Lookup(ExpectedAuthorName);

            results.Should().NotBeNullOrEmpty("the metadata provider must return at least one match");
            results.Should().Contain(
                a => string.Equals(a.AuthorName, ExpectedAuthorName, StringComparison.OrdinalIgnoreCase),
                $"result set must include '{ExpectedAuthorName}'");
        }

        [Test]
        [Order(2)]
        public void add_monitored_author_to_library()
        {
            var added = AddExpectedAuthor();

            added.Should().NotBeNull();
            added.Id.Should().BeGreaterThan(0, "persisted author must have a non-zero database id");
            added.Monitored.Should().BeTrue("author was added as monitored");
            added.Path.Should().Be(Path.Combine(AuthorRootFolder, added.AuthorName));
        }

        [Test]
        [Order(3)]
        public void refresh_author_populates_books_in_library()
        {
            var author = AddExpectedAuthor();

            Commands.PostAndWait(new RefreshAuthorCommand(author.Id));
            Commands.WaitAll();

            var books = Books.GetBooksInAuthor(author.Id);
            books.Should().NotBeNullOrEmpty($"at least one book must exist for author {ExpectedAuthorName} after refresh");
        }

        [Test]
        [Order(4)]
        public void imported_book_file_is_listed_and_rename_preview_is_available()
        {
            var author = AddExpectedAuthor();
            EnsureDownloadClient(enabled: false);

            var book = GetFirstBook(author.Id);

            EnsureBookFile(author, book.Id, book.ForeignEditionId, Quality.MOBI);

            var bookFiles = BookFiles.GetByBook(book.Id);
            bookFiles.Should().ContainSingle("the manual import should have created one imported book file");

            var previews = new RenameBookClient(RestClient, ApiKey).GetPreview(author.Id, book.Id);
            previews.Should().NotBeNullOrEmpty("the imported file should be eligible for rename preview");
            previews.First().ExistingPath.Should().Be(bookFiles.First().Path);
            previews.First().NewPath.Should().NotBe(bookFiles.First().Path);
        }

        [Test]
        [Order(5)]
        public void delete_author_removes_it_from_library()
        {
            var author = AddExpectedAuthor();

            Author.Delete(author.Id);
            Commands.WaitAll();

            Author.All().Should().NotContain(
                a => a.Id == author.Id,
                "deleted author must not appear in the library");

            _addedAuthorId = 0;
        }
    }
}
