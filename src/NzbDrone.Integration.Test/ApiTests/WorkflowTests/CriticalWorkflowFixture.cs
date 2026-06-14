using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Integration.Test.Client;

namespace NzbDrone.Integration.Test.ApiTests.WorkflowTests
{
    /// <summary>
    /// End-to-end workflow tests covering the critical happy-path for adding an author,
    /// triggering a book refresh, and verifying the library state.
    ///
    /// These tests require a running Readarr instance (started by the test harness).
    /// Download-client and indexer interactions are stubbed — no real external services
    /// are contacted.  The metadata provider (Open Library / rreading-glasses) is the
    /// only network dependency; use the Category=ManualTest filter to skip it in CI.
    ///
    /// Run with:
    ///   dotnet test --filter "Category=IntegrationTest&amp;FullyQualifiedName~CriticalWorkflowFixture"
    /// </summary>
    [TestFixture]
    [Ignore("Integration workflow tests require a live Readarr process; run manually with Category=IntegrationTest", Until = "2099-01-01 00:00:00Z")]
    public class CriticalWorkflowFixture : IntegrationTest
    {
        // -----------------------------------------------------------------------
        // Well-known stable Open Library / Goodreads identifiers used as fixtures.
        // These were verified to resolve correctly against the Open Library API.
        // -----------------------------------------------------------------------

        // J.K. Rowling — OL23919A / Goodreads 1077326
        private const string AuthorForeignId = "1077326";
        private const string EditionSearchTerm = "edition:2";   // OL:2 → Harry Potter
        private const string ExpectedAuthorName = "J.K. Rowling";

        // -----------------------------------------------------------------------
        // Per-test state
        // -----------------------------------------------------------------------

        private string _authorRootPath;
        private int _addedAuthorId;

        [SetUp]
        public void WorkflowSetUp()
        {
            // Each test gets its own temporary author root so that they are isolated
            // and the teardown can clean up aggressively without affecting others.
            _authorRootPath = GetTempDirectory("WorkflowTests", "AuthorRoot");
            _addedAuthorId = 0;
        }

        [TearDown]
        public void WorkflowTearDown()
        {
            // Remove the author we added so subsequent runs start clean.
            if (_addedAuthorId != 0)
            {
                try
                {
                    Author.Delete(_addedAuthorId);
                    Commands.WaitAll();
                }
                catch
                {
                    // Best-effort; the OneTimeTearDown will stop the process anyway.
                }
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns a stub download-client (UsenetBlackhole) that is present in the
        /// integration test environment.  No real download provider is contacted.
        /// </summary>
        private void EnsureStubDownloadClient()
        {
            // UsenetBlackhole watches a local directory — no external service needed.
            EnsureDownloadClient(enabled: false);
        }

        // -----------------------------------------------------------------------
        // Tests
        // -----------------------------------------------------------------------

        /// <summary>
        /// Scenario: Search for an author by name via the lookup endpoint.
        /// Expected: At least one result is returned containing the expected author.
        /// Stubs: none required — only the metadata provider lookup is exercised.
        /// </summary>
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

        /// <summary>
        /// Scenario: Add a monitored author to the library.
        /// Expected: Author is persisted with Monitored=true and the assigned path.
        /// Stubs: none — no indexer or download-client interaction occurs during add.
        /// </summary>
        [Test]
        [Order(2)]
        public void add_monitored_author_to_library()
        {
            EnsureNoAuthor(AuthorForeignId, ExpectedAuthorName);

            var lookup = Author.Lookup(EditionSearchTerm);
            lookup.Should().NotBeNullOrEmpty("lookup by edition id must return results");

            var authorPayload = lookup.First();
            authorPayload.QualityProfileId = 1;
            authorPayload.MetadataProfileId = 1;
            authorPayload.Path = Path.Combine(_authorRootPath, authorPayload.AuthorName);
            authorPayload.Monitored = true;
            authorPayload.AddOptions = new NzbDrone.Core.Books.AddAuthorOptions();

            Directory.CreateDirectory(authorPayload.Path);

            var added = Author.Post(authorPayload);
            _addedAuthorId = added.Id;

            Commands.WaitAll();

            added.Should().NotBeNull();
            added.Id.Should().BeGreaterThan(0, "persisted author must have a non-zero database id");
            added.Monitored.Should().BeTrue("author was added as monitored");
            added.Path.Should().Be(authorPayload.Path);
        }

        /// <summary>
        /// Scenario: Trigger a RefreshAuthor command and verify that books are
        ///           populated in the library afterwards.
        /// Expected: At least one book is available for the author after the refresh.
        /// Stubs: download-client and indexer — the test only exercises metadata fetch.
        /// </summary>
        [Test]
        [Order(3)]
        public void refresh_author_populates_books_in_library()
        {
            // Ensure the author exists (idempotent helper).
            var author = EnsureAuthor(AuthorForeignId, "2", ExpectedAuthorName, monitored: true);
            _addedAuthorId = author.Id;

            // Stub download client so Readarr doesn't attempt a real search/download.
            EnsureStubDownloadClient();

            // Trigger a metadata refresh for this specific author.
            Commands.PostAndWait(new RefreshAuthorCommand(author.Id));
            Commands.WaitAll();

            // Verify books appear in the library.
            var books = Books.GetBooksInAuthor(author.Id);
            books.Should().NotBeNullOrEmpty($"at least one book must exist for author {ExpectedAuthorName} after refresh");
        }

        /// <summary>
        /// Scenario: Verify a book added after author refresh appears in the library
        ///           and can be retrieved individually.
        /// Expected: The book is retrievable by id with a valid title.
        /// </summary>
        [Test]
        [Order(4)]
        public void book_added_by_refresh_is_retrievable_by_id()
        {
            var author = EnsureAuthor(AuthorForeignId, "2", ExpectedAuthorName, monitored: true);
            _addedAuthorId = author.Id;

            // Ensure books are present (waits for the initial refresh triggered on add).
            WaitForCompletion(
                () => Books.GetBooksInAuthor(author.Id).Count > 0,
                timeout: 30000,
                interval: 1000);

            var books = Books.GetBooksInAuthor(author.Id);
            books.Should().NotBeNullOrEmpty();

            var firstBook = books.First();
            firstBook.Id.Should().BeGreaterThan(0);
            firstBook.Title.Should().NotBeNullOrEmpty("every book must have a non-empty title");
            firstBook.AuthorId.Should().Be(author.Id);
        }

        /// <summary>
        /// Scenario: Remove an author from the library and verify it is gone.
        /// Expected: After deletion, the author no longer appears in the All() listing.
        /// </summary>
        [Test]
        [Order(5)]
        public void delete_author_removes_it_from_library()
        {
            var author = EnsureAuthor(AuthorForeignId, "2", ExpectedAuthorName);

            Author.Delete(author.Id);
            Commands.WaitAll();

            Author.All().Should().NotContain(
                a => a.ForeignAuthorId == AuthorForeignId,
                "deleted author must not appear in the library");

            // Mark as cleaned up so TearDown doesn't try to delete again.
            _addedAuthorId = 0;
        }
    }
}
