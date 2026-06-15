using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.MediaFiles.BookImport.Manual;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Qualities;
using NzbDrone.Integration.Test.Client;
using Readarr.Api.V3.Author;
using Readarr.Api.V3.Books;
using Readarr.Api.V3.RootFolders;

namespace NzbDrone.Integration.Test.ApiTests.WorkflowTests
{
    /// <summary>
    /// End-to-end workflow tests covering lookup, author add, book add, manual import,
    /// duplicate suppression, rename preview, and rename publication through the live API.
    /// </summary>
    [TestFixture]
    public class CriticalWorkflowFixture : IntegrationTest
    {
        private const string ExpectedAuthorName = "Ursula Kroeber Le Guin";
        private const string ExpectedIsbn = "9780547773742";
        private const string ExpectedBookTitle = "A Wizard of Earthsea";

        private readonly List<int> _createdAuthorIds = new List<int>();
        private int _rootFolderId;

        [SetUp]
        public void WorkflowSetUp()
        {
            _createdAuthorIds.Clear();

            foreach (var existing in Author.All().Where(a => string.Equals(a.AuthorName, ExpectedAuthorName, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                Author.Delete(existing.Id);
            }

            foreach (var existingRootFolder in RootFolders.All().Where(r => string.Equals(r.Path, AuthorRootFolder, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                RootFolders.Delete(existingRootFolder.Id);
            }

            var rootFolder = RootFolders.Post(new RootFolderResource
            {
                Name = "CriticalWorkflowTestLibrary",
                Path = AuthorRootFolder,
                DefaultMetadataProfileId = 1,
                DefaultQualityProfileId = 1,
                DefaultMonitorOption = MonitorTypes.All
            });
            _rootFolderId = rootFolder.Id;

            Commands.WaitAll();

            var authorFolder = Path.Combine(AuthorRootFolder, ExpectedAuthorName);
            if (Directory.Exists(authorFolder))
            {
                Directory.Delete(authorFolder, true);
            }
        }

        [TearDown]
        public void WorkflowTearDown()
        {
            foreach (var authorId in _createdAuthorIds.Distinct().Reverse())
            {
                try
                {
                    Author.Delete(authorId);
                    Commands.WaitAll();
                }
                catch
                {
                    // Best-effort cleanup; the harness tears the process down after the fixture.
                }
            }

            _createdAuthorIds.Clear();

            if (_rootFolderId > 0)
            {
                try
                {
                    RootFolders.Delete(_rootFolderId);
                }
                catch
                {
                    // Best-effort cleanup; the fixture-specific root folder should not survive the run.
                }
                finally
                {
                    _rootFolderId = 0;
                }
            }
        }

        private AuthorResource AddExpectedAuthor(bool monitored = true)
        {
            var results = BookLookup.Lookup("isbn:" + ExpectedIsbn);
            results.Should().NotBeNullOrEmpty($"ISBN lookup for '{ExpectedIsbn}' must return at least one result");

            var bookPayload = results.FirstOrDefault(b => string.Equals(b.Title, ExpectedBookTitle, StringComparison.OrdinalIgnoreCase));
            bookPayload.Should().NotBeNull($"ISBN lookup for '{ExpectedIsbn}' must include '{ExpectedBookTitle}'");

            var authorLookup = Author.Lookup("edition:" + bookPayload.ForeignEditionId);
            authorLookup.Should().NotBeNullOrEmpty($"ISBN search for '{ExpectedIsbn}' must resolve an author");

            var authorTemplate = authorLookup.FirstOrDefault(a => string.Equals(a.AuthorName, ExpectedAuthorName, StringComparison.OrdinalIgnoreCase))
                                 ?? authorLookup.First();
            authorTemplate.AuthorName.Should().NotBeNullOrWhiteSpace("author lookup must return a populated author name");
            authorTemplate.ForeignAuthorId.Should().NotBeNullOrWhiteSpace("author lookup must return a populated foreign author id");

            var newAuthor = new AuthorResource
            {
                AuthorName = authorTemplate.AuthorName,
                ForeignAuthorId = authorTemplate.ForeignAuthorId,
                QualityProfileId = 1,
                MetadataProfileId = 1,
                Path = Path.Combine(AuthorRootFolder, authorTemplate.AuthorName),
                Monitored = monitored,
                AddOptions = new NzbDrone.Core.Books.AddAuthorOptions()
            };

            Directory.CreateDirectory(newAuthor.Path);

            var added = Author.Post(newAuthor);
            _createdAuthorIds.Add(added.Id);

            Commands.WaitAll();

            return added;
        }

        private BookResource AddExpectedBook(AuthorResource author)
        {
            var results = BookLookup.Lookup("isbn:" + ExpectedIsbn);
            results.Should().NotBeNullOrEmpty($"ISBN lookup for '{ExpectedIsbn}' must return at least one result");

            var bookPayload = results.FirstOrDefault(b => string.Equals(b.Title, ExpectedBookTitle, StringComparison.OrdinalIgnoreCase));
            bookPayload.Should().NotBeNull($"ISBN lookup for '{ExpectedIsbn}' must include '{ExpectedBookTitle}'");

            bookPayload.Author = new AuthorResource
            {
                Id = author.Id,
                AuthorName = author.AuthorName,
                ForeignAuthorId = author.ForeignAuthorId,
                Path = author.Path,
                RootFolderPath = AuthorRootFolder,
                QualityProfileId = author.QualityProfileId,
                MetadataProfileId = author.MetadataProfileId,
                Monitored = author.Monitored
            };
            bookPayload.Editions = new List<EditionResource>
            {
                new EditionResource
                {
                    ForeignEditionId = bookPayload.ForeignEditionId,
                    Title = bookPayload.Title,
                    TitleSlug = bookPayload.ForeignEditionId,
                    Monitored = true
                }
            };
            bookPayload.AddOptions = new NzbDrone.Core.Books.AddBookOptions();

            var added = Books.Post(bookPayload);
            Commands.WaitAll();

            return added;
        }

        private BookResource GetFirstBook(int authorId)
        {
            WaitForCompletion(() => Books.GetBooksInAuthor(authorId).Any(), timeout: 30000, interval: 1000);
            return Books.GetBooksInAuthor(authorId).First();
        }

        private string CreateImportFile(string bookTitle, string fileName)
        {
            var sourceRoot = GetTempDirectory("Readarr-Item3", Guid.NewGuid().ToString("N"), ExpectedAuthorName, bookTitle);

            var path = Path.Combine(sourceRoot, fileName);

            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var mimetypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using (var writer = new StreamWriter(mimetypeEntry.Open()))
                {
                    writer.Write("application/epub+zip");
                }

                var containerEntry = archive.CreateEntry("META-INF/container.xml");
                using (var writer = new StreamWriter(containerEntry.Open()))
                {
                    writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<container version=""1.0"" xmlns=""urn:oasis:names:tc:opendocument:xmlns:container"">
  <rootfiles>
    <rootfile full-path=""OEBPS/content.opf"" media-type=""application/oebps-package+xml"" />
  </rootfiles>
</container>");
                }

                var packageEntry = archive.CreateEntry("OEBPS/content.opf");
                using (var writer = new StreamWriter(packageEntry.Open()))
                {
                    writer.Write($@"<?xml version=""1.0"" encoding=""UTF-8""?>
<package version=""3.0"" unique-identifier=""BookId"" xmlns=""http://www.idpf.org/2007/opf"" xmlns:dc=""http://purl.org/dc/elements/1.1/"" xmlns:opf=""http://www.idpf.org/2007/opf"">
  <metadata>
    <dc:identifier id=""BookId"" opf:scheme=""ISBN"">{ExpectedIsbn}</dc:identifier>
    <dc:title>{bookTitle}</dc:title>
    <dc:creator>{ExpectedAuthorName}</dc:creator>
    <dc:language>en</dc:language>
    <dc:publisher>Test Publisher</dc:publisher>
    <dc:description>Workflow test EPUB</dc:description>
  </metadata>
</package>");
                }
            }

            return path;
        }

        private void EnsureRenameBooksEnabled()
        {
            var config = NamingConfig.GetSingle();
            config.RenameBooks = true;
            config.ReplaceIllegalCharacters = true;
            config.AuthorFolderFormat = "{Author Name}";
            config.StandardBookFormat = "{Author Name}/{Book Title}{ (PartNumber)}";

            NamingConfig.Put(config);
        }

        private void ImportViaCommand(AuthorResource author, BookResource book, string path, bool replaceExistingFiles)
        {
            Commands.PostAndWait(new ManualImportCommand
            {
                Files = new List<ManualImportFile>
                {
                    new ManualImportFile
                    {
                        Path = path,
                        AuthorId = author.Id,
                        BookId = book.Id,
                        ForeignEditionId = book.ForeignEditionId,
                        Quality = new QualityModel(Quality.MOBI)
                    }
                },
                ImportMode = ImportMode.Auto,
                ReplaceExistingFiles = replaceExistingFiles
            });

            Commands.WaitAll();
        }

        [Test]
        [Order(1)]
        public void lookup_book_by_isbn_returns_results()
        {
            var results = BookLookup.Lookup("isbn:" + ExpectedIsbn);

            results.Should().NotBeNullOrEmpty("ISBN lookup must return at least one book");
            results.Should().Contain(
                b => string.Equals(b.Title, ExpectedBookTitle, StringComparison.OrdinalIgnoreCase),
                $"ISBN lookup must include '{ExpectedBookTitle}'");
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
        public void add_book_from_lookup_populates_books_in_library()
        {
            var author = AddExpectedAuthor();
            var book = AddExpectedBook(author);

            var books = Books.GetBooksInAuthor(author.Id);
            books.Should().NotBeNullOrEmpty($"at least one book must exist for author {ExpectedAuthorName} after lookup add");
            books.Should().Contain(b => string.Equals(b.Title, ExpectedBookTitle, StringComparison.OrdinalIgnoreCase));
            books.Should().ContainSingle(b => b.Id == book.Id);
        }

        [Test]
        [Order(4)]
        public void manual_import_preview_can_identify_the_target_book()
        {
            var author = AddExpectedAuthor();

            var book = AddExpectedBook(author);
            var importPath = CreateImportFile(book.Title, $"{ExpectedAuthorName} - {book.Title}.epub");

            var preview = ManualImport.GetMediaFiles(Path.GetDirectoryName(importPath), author.Id, filterExistingFiles: false, replaceExistingFiles: true);

            preview.Should().NotBeEmpty();
            preview.Should().ContainSingle(item =>
                item.Path.Equals(importPath, StringComparison.OrdinalIgnoreCase) &&
                item.Author != null &&
                item.Author.Id == author.Id &&
                item.Book != null &&
                item.Book.Id == book.Id &&
                item.ForeignEditionId == book.ForeignEditionId);
        }

        [Test]
        [Order(5)]
        public void manual_import_command_imports_file_and_records_history()
        {
            var author = AddExpectedAuthor();

            var book = AddExpectedBook(author);
            var importPath = CreateImportFile(book.Title, $"{ExpectedAuthorName} - {book.Title}.epub");

            ImportViaCommand(author, book, importPath, replaceExistingFiles: true);

            var files = BookFiles.GetByBook(book.Id);
            files.Should().ContainSingle();

            History.GetByAuthor(author.Id, book.Id, EntityHistoryEventType.BookFileImported)
                .Should()
                .NotBeEmpty("manual import must be recorded in history");
        }

        [Test]
        [Order(6)]
        public void duplicate_manual_import_does_not_create_duplicate_book_files()
        {
            var author = AddExpectedAuthor();

            var book = AddExpectedBook(author);
            var firstImport = CreateImportFile(book.Title, $"{ExpectedAuthorName} - {book.Title}.epub");
            var secondImport = CreateImportFile(book.Title, $"{ExpectedAuthorName} - {book.Title} copy.epub");

            ImportViaCommand(author, book, firstImport, replaceExistingFiles: false);
            ImportViaCommand(author, book, secondImport, replaceExistingFiles: false);

            BookFiles.GetByBook(book.Id)
                .Should()
                .ContainSingle("re-importing the same edition should not create duplicate book files");
        }

        [Test]
        [Order(7)]
        public void rename_preview_and_command_update_the_imported_file()
        {
            var author = AddExpectedAuthor();

            var book = AddExpectedBook(author);
            var importPath = CreateImportFile(book.Title, $"{ExpectedAuthorName} - {book.Title}.epub");

            ImportViaCommand(author, book, importPath, replaceExistingFiles: true);

            var beforeRename = BookFiles.GetByBook(book.Id).Single();
            EnsureRenameBooksEnabled();
            var previews = new RenameBookClient(RestClient, ApiKey).GetPreview(author.Id, book.Id);

            previews.Should().NotBeNullOrEmpty("the imported file should be eligible for rename preview");
            previews.First().ExistingPath.Should().Be(beforeRename.Path);
            previews.First().NewPath.Should().NotBe(beforeRename.Path);

            Commands.PostAndWait(new RenameAuthorCommand
            {
                AuthorIds = new List<int> { author.Id }
            });
            Commands.WaitAll();

            var afterRename = BookFiles.GetByBook(book.Id).Single();
            afterRename.Path.Should().NotBe(beforeRename.Path, "the rename command should move the file to the configured naming format");

            History.GetByAuthor(author.Id, book.Id, EntityHistoryEventType.BookFileRenamed)
                .Should()
                .NotBeEmpty("rename must be recorded in history");
        }

        [Test]
        [Order(8)]
        public void delete_author_removes_it_from_library()
        {
            var author = AddExpectedAuthor();

            Author.Delete(author.Id);
            Commands.WaitAll();

            Author.All().Should().NotContain(
                a => a.Id == author.Id,
                "deleted author must not appear in the library");

            _createdAuthorIds.Remove(author.Id);
        }
    }
}
