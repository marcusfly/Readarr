using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.BookImport;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ImportAttemptServiceFixture : CoreTest<ImportAttemptService>
    {
        private const string SourcePath = @"C:\downloads\Author - Book (2020).epub";
        private const string DestPath = @"C:\library\Author\Book (2020)\Author - Book (2020).epub";
        private const long SourceSize = 123456L;

        // -----------------------------------------------------------------------
        // Begin
        // -----------------------------------------------------------------------
        [Test]
        public void begin_should_insert_pending_attempt()
        {
            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.Insert(It.IsAny<ImportAttempt>()))
                  .Returns<ImportAttempt>(a =>
                  {
                      a.Id = 1;
                      return a;
                  });

            var result = Subject.Begin(SourcePath, DestPath, SourceSize, isDryRun: false);

            result.Status.Should().Be(ImportAttemptStatus.Pending);
            result.SourcePath.Should().Be(SourcePath);
            result.DestinationPath.Should().Be(DestPath);
            result.SourceSize.Should().Be(SourceSize);
            result.IsDryRun.Should().BeFalse();

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Insert(It.Is<ImportAttempt>(a =>
                      a.Status == ImportAttemptStatus.Pending &&
                      a.SourcePath == SourcePath &&
                      a.SourceSize == SourceSize)), Times.Once());
        }

        [Test]
        public void begin_dry_run_should_set_is_dry_run_flag()
        {
            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.Insert(It.IsAny<ImportAttempt>()))
                  .Returns<ImportAttempt>(a =>
                  {
                      a.Id = 2;
                      return a;
                  });

            var result = Subject.Begin(SourcePath, DestPath, SourceSize, isDryRun: true);

            result.IsDryRun.Should().BeTrue();
        }

        // -----------------------------------------------------------------------
        // MarkInProgress
        // -----------------------------------------------------------------------
        [Test]
        public void mark_in_progress_should_update_status()
        {
            var attempt = new ImportAttempt { Id = 1, Status = ImportAttemptStatus.Pending };

            Subject.MarkInProgress(attempt);

            attempt.Status.Should().Be(ImportAttemptStatus.InProgress);
            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a => a.Status == ImportAttemptStatus.InProgress)), Times.Once());
        }

        // -----------------------------------------------------------------------
        // MarkCompleted
        // -----------------------------------------------------------------------
        [Test]
        public void mark_completed_should_set_status_and_finish_time()
        {
            var attempt = new ImportAttempt { Id = 1, Status = ImportAttemptStatus.InProgress };

            Subject.MarkCompleted(attempt);

            attempt.Status.Should().Be(ImportAttemptStatus.Completed);
            attempt.FinishedAt.Should().NotBeNull();
        }

        // -----------------------------------------------------------------------
        // MarkFailed
        // -----------------------------------------------------------------------
        [Test]
        public void mark_failed_should_set_status_and_error_message()
        {
            var attempt = new ImportAttempt { Id = 1, Status = ImportAttemptStatus.InProgress };

            Subject.MarkFailed(attempt, "disk full");

            attempt.Status.Should().Be(ImportAttemptStatus.Failed);
            attempt.ErrorMessage.Should().Be("disk full");
            attempt.FinishedAt.Should().NotBeNull();
        }

        // -----------------------------------------------------------------------
        // MarkRolledBack
        // -----------------------------------------------------------------------
        [Test]
        public void mark_rolled_back_should_set_status()
        {
            var attempt = new ImportAttempt { Id = 1, Status = ImportAttemptStatus.InProgress };

            Subject.MarkRolledBack(attempt);

            attempt.Status.Should().Be(ImportAttemptStatus.RolledBack);
            attempt.FinishedAt.Should().NotBeNull();
        }

        // -----------------------------------------------------------------------
        // Startup crash recovery — Handle(ApplicationStartedEvent)
        // -----------------------------------------------------------------------
        [Test]
        public void handle_startup_should_mark_completed_when_destination_exists()
        {
            var orphan = new ImportAttempt
            {
                Id = 10,
                SourcePath = SourcePath,
                DestinationPath = DestPath,
                SourceSize = SourceSize,
                Status = ImportAttemptStatus.InProgress
            };

            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt> { orphan });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FileExists(DestPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.GetFileSize(DestPath))
                  .Returns(SourceSize);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFileWithPath(DestPath))
                  .Returns(new BookFile { Path = DestPath, Size = SourceSize });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a =>
                      a.Id == 10 && a.Status == ImportAttemptStatus.Completed)), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void handle_startup_should_mark_rolled_back_when_destination_missing()
        {
            var orphan = new ImportAttempt
            {
                Id = 11,
                SourcePath = SourcePath,
                DestinationPath = DestPath,
                SourceSize = SourceSize,
                Status = ImportAttemptStatus.InProgress
            };

            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt> { orphan });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FileExists(DestPath))
                  .Returns(false);

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a =>
                      a.Id == 11 &&
                      a.Status == ImportAttemptStatus.RolledBack &&
                      a.ErrorMessage == "Destination file missing or did not match the recorded source file size.")), Times.Once());

            ExceptionVerification.ExpectedWarns(2);
        }

        [Test]
        public void handle_startup_should_mark_rolled_back_when_destination_is_not_tracked_in_database()
        {
            var orphan = new ImportAttempt
            {
                Id = 12,
                SourcePath = SourcePath,
                DestinationPath = DestPath,
                SourceSize = SourceSize,
                Status = ImportAttemptStatus.InProgress
            };

            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt> { orphan });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FileExists(DestPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.GetFileSize(DestPath))
                  .Returns(SourceSize);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFileWithPath(DestPath))
                  .Returns((BookFile)null);

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a =>
                      a.Id == 12 &&
                      a.Status == ImportAttemptStatus.RolledBack &&
                      a.ErrorMessage == "Destination file missing or did not match the recorded source file size.")), Times.Once());

            ExceptionVerification.ExpectedWarns(3);
        }

        [Test]
        public void handle_startup_should_mark_rolled_back_when_destination_size_does_not_match_source_size()
        {
            var orphan = new ImportAttempt
            {
                Id = 13,
                SourcePath = SourcePath,
                DestinationPath = DestPath,
                SourceSize = SourceSize,
                Status = ImportAttemptStatus.InProgress
            };

            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt> { orphan });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FileExists(DestPath))
                  .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.GetFileSize(DestPath))
                  .Returns(SourceSize - 1);

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a =>
                      a.Id == 13 &&
                      a.Status == ImportAttemptStatus.RolledBack &&
                      a.ErrorMessage == "Destination file missing or did not match the recorded source file size.")), Times.Once());

            ExceptionVerification.ExpectedWarns(3);
        }

        [Test]
        public void handle_startup_should_treat_destination_exists_as_completed_when_legacy_source_size_is_missing()
        {
            var orphan = new ImportAttempt
            {
                Id = 14,
                SourcePath = SourcePath,
                DestinationPath = DestPath,
                SourceSize = 0,
                Status = ImportAttemptStatus.InProgress
            };

            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt> { orphan });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FileExists(DestPath))
                  .Returns(true);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFileWithPath(DestPath))
                  .Returns(new BookFile { Path = DestPath, Size = SourceSize });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a =>
                      a.Id == 14 && a.Status == ImportAttemptStatus.Completed)), Times.Once());

            Mocker.GetMock<IDiskProvider>()
                  .Verify(d => d.GetFileSize(It.IsAny<string>()), Times.Never());

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void handle_startup_should_do_nothing_when_no_orphans()
        {
            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt>());

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.IsAny<ImportAttempt>()), Times.Never());
        }

        [Test]
        public void handle_startup_should_continue_after_individual_recovery_exception()
        {
            var orphan1 = new ImportAttempt { Id = 20, SourcePath = SourcePath, DestinationPath = DestPath, SourceSize = SourceSize, Status = ImportAttemptStatus.InProgress };
            var orphan2 = new ImportAttempt { Id = 21, SourcePath = SourcePath, DestinationPath = DestPath, SourceSize = SourceSize, Status = ImportAttemptStatus.InProgress };

            Mocker.GetMock<IImportAttemptRepository>()
                  .Setup(r => r.FindInProgress())
                  .Returns(new List<ImportAttempt> { orphan1, orphan2 });

            // First FileExists call throws; second succeeds.
            var callCount = 0;
            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.FileExists(It.IsAny<string>()))
                  .Returns(() =>
                  {
                      callCount++;
                      if (callCount == 1)
                      {
                          throw new Exception("simulated disk error");
                      }

                      return true;
                  });

            Mocker.GetMock<IDiskProvider>()
                  .Setup(d => d.GetFileSize(DestPath))
                  .Returns(SourceSize);

            Mocker.GetMock<IMediaFileService>()
                  .Setup(s => s.GetFileWithPath(DestPath))
                  .Returns(new BookFile { Path = DestPath, Size = SourceSize });

            Assert.DoesNotThrow(() => Subject.Handle(new ApplicationStartedEvent()));

            // Second orphan should still be processed.
            Mocker.GetMock<IImportAttemptRepository>()
                  .Verify(r => r.Update(It.Is<ImportAttempt>(a =>
                      a.Id == 21 && a.Status == ImportAttemptStatus.Completed)), Times.Once());

            ExceptionVerification.ExpectedWarns(1);
            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
