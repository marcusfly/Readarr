using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    /// <summary>
    /// Tests for the dry-run support added to ImportApprovedBooks.
    /// A dry-run must log the import plan without moving or copying any files.
    /// </summary>
    [TestFixture]
    public class ImportApprovedBooksDryRunFixture : CoreTest<ImportApprovedBooks>
    {
        private List<ImportDecision<LocalBook>> _approvedDecisions;

        [SetUp]
        public void Setup()
        {
            _approvedDecisions = new List<ImportDecision<LocalBook>>();

            var author = Builder<Author>.CreateNew()
                .With(e => e.QualityProfile = new QualityProfile { Items = Qualities.QualityFixture.GetDefaultQualities() })
                .With(s => s.Path = @"C:\Test\Music\TestAuthor".AsOsAgnostic())
                .Build();

            var book = Builder<Book>.CreateNew()
                .With(e => e.Author = author)
                .Build();

            var edition = Builder<Edition>.CreateNew()
                .With(e => e.Book = book)
                .With(e => e.Monitored = true)
                .Build();

            book.Editions = new List<Edition> { edition };

            var rootFolder = Builder<RootFolder>.CreateNew()
                .With(r => r.IsCalibreLibrary = false)
                .Build();

            _approvedDecisions.Add(new ImportDecision<LocalBook>(
                new LocalBook
                {
                    Author = author,
                    Book = book,
                    Edition = edition,
                    Part = 1,
                    Path = Path.Combine(author.Path, "TestAuthor - 01 - Chapter.mp3"),
                    Quality = new QualityModel(Quality.MP3),
                    FileTrackInfo = new ParsedTrackInfo { ReleaseGroup = "DRONE" }
                }));

            Mocker.GetMock<IUpgradeMediaFiles>()
                  .Setup(s => s.UpgradeBookFile(It.IsAny<BookFile>(), It.IsAny<LocalBook>(), It.IsAny<bool>()))
                  .Returns(new BookFileMoveResult());

            Mocker.GetMock<IEditionService>()
                  .Setup(s => s.SetMonitored(edition))
                  .Returns(new List<Edition> { edition });

            Mocker.GetMock<IRootFolderService>()
                  .Setup(s => s.GetBestRootFolder(It.IsAny<string>()))
                  .Returns(rootFolder);

            // Stub import attempt service so Begin returns a usable object.
            Mocker.GetMock<IImportAttemptService>()
                  .Setup(s => s.Begin(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                  .Returns(new ImportAttempt { Id = 1, Status = ImportAttemptStatus.Pending });
        }

        [Test]
        public void dry_run_should_not_call_upgrade_book_file()
        {
            Subject.Import(_approvedDecisions, false, dryRun: true);

            Mocker.GetMock<IUpgradeMediaFiles>()
                  .Verify(s => s.UpgradeBookFile(It.IsAny<BookFile>(), It.IsAny<LocalBook>(), It.IsAny<bool>()),
                          Times.Never(),
                          "a dry-run must not move or copy files");
        }

        [Test]
        public void dry_run_should_return_one_result_per_approved_decision()
        {
            var results = Subject.Import(_approvedDecisions, false, dryRun: true);

            results.Where(r => r.Result == ImportResultType.Imported).Should().HaveCount(_approvedDecisions.Count);
        }

        [Test]
        public void dry_run_should_record_a_dry_run_attempt_for_each_file()
        {
            Subject.Import(_approvedDecisions, false, dryRun: true);

            Mocker.GetMock<IImportAttemptService>()
                  .Verify(s => s.Begin(It.IsAny<string>(), It.IsAny<string>(), true),
                          Times.Exactly(_approvedDecisions.Count));
        }

        [Test]
        public void normal_import_should_call_upgrade_book_file()
        {
            Subject.Import(_approvedDecisions, false, dryRun: false);

            Mocker.GetMock<IUpgradeMediaFiles>()
                  .Verify(s => s.UpgradeBookFile(It.IsAny<BookFile>(), It.IsAny<LocalBook>(), It.IsAny<bool>()),
                          Times.Once());
        }

        [Test]
        public void normal_import_should_begin_non_dry_run_attempt()
        {
            // Reset so the attempt returned by Begin starts Pending (required before MarkInProgress).
            Mocker.GetMock<IImportAttemptService>()
                  .Setup(s => s.Begin(It.IsAny<string>(), It.IsAny<string>(), false))
                  .Returns(new ImportAttempt { Id = 2, Status = ImportAttemptStatus.Pending });

            Subject.Import(_approvedDecisions, false, dryRun: false);

            Mocker.GetMock<IImportAttemptService>()
                  .Verify(s => s.Begin(It.IsAny<string>(), It.IsAny<string>(), false), Times.Once());
        }
    }
}
