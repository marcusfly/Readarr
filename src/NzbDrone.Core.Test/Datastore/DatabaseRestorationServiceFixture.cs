using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class DatabaseRestorationServiceFixture
    {
        private Mock<IDiskProvider> _diskProvider;
        private Mock<IAppFolderInfo> _appFolderInfo;
        private DatabaseRestorationService _subject;
        private string _dbPath;
        private string _dbRestorePath;
        private string _dbBackupPath;

        [SetUp]
        public void Setup()
        {
            _diskProvider = new Mock<IDiskProvider>();
            _appFolderInfo = new Mock<IAppFolderInfo>();
            _appFolderInfo.SetupGet(x => x.AppDataFolder).Returns(Path.Combine(Path.GetTempPath(), "readarr_restore_test"));
            _subject = new DatabaseRestorationService(_diskProvider.Object, _appFolderInfo.Object);

            _dbPath = _appFolderInfo.Object.GetDatabase();
            _dbRestorePath = _appFolderInfo.Object.GetDatabaseRestore();
            _dbBackupPath = _dbPath + ".restore-backup";
        }

        [Test]
        public void Restore_should_replace_database_from_restore_marker_and_cleanup_temporary_backup()
        {
            var seq = new MockSequence();

            _diskProvider
                .Setup(x => x.FileExists(_dbRestorePath))
                .Returns(true);

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath + "-shm"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath + "-wal"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath + "-journal"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.FileExists(_dbPath))
                .Returns(true);

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.MoveFile(_dbPath, _dbBackupPath, true));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.MoveFile(_dbRestorePath, _dbPath, false));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbBackupPath));

            _subject.Restore();

            _diskProvider.Verify(x => x.MoveFile(_dbPath, _dbBackupPath, true), Times.Once());
            _diskProvider.Verify(x => x.MoveFile(_dbRestorePath, _dbPath, false), Times.Once());
            _diskProvider.Verify(x => x.DeleteFile(_dbBackupPath), Times.Once());
            _diskProvider.Verify(x => x.DeleteFile(_dbPath), Times.Never());
        }

        [Test]
        public void Restore_should_put_original_database_back_if_restore_move_fails()
        {
            var seq = new MockSequence();

            _diskProvider
                .Setup(x => x.FileExists(_dbRestorePath))
                .Returns(true);

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath + "-shm"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath + "-wal"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath + "-journal"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.FileExists(_dbPath))
                .Returns(true);

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.MoveFile(_dbPath, _dbBackupPath, true));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.MoveFile(_dbRestorePath, _dbPath, false))
                .Throws(new IOException("restore move failed"));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.DeleteFile(_dbPath));

            _diskProvider
                .InSequence(seq)
                .Setup(x => x.MoveFile(_dbBackupPath, _dbPath, true));

            var act = () => _subject.Restore();

            act.Should().Throw<IOException>().WithMessage("restore move failed");

            _diskProvider.Verify(x => x.MoveFile(_dbBackupPath, _dbPath, true), Times.Once());
            _diskProvider.Verify(x => x.DeleteFile(_dbBackupPath), Times.Never());
        }
    }
}
