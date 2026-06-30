using System;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation;

namespace NzbDrone.Core.Datastore
{
    public interface IRestoreDatabase
    {
        void Restore();
    }

    public class DatabaseRestorationService : IRestoreDatabase
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(DatabaseRestorationService));

        public DatabaseRestorationService(IDiskProvider diskProvider, IAppFolderInfo appFolderInfo)
        {
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
        }

        public void Restore()
        {
            var dbRestorePath = _appFolderInfo.GetDatabaseRestore();

            if (!_diskProvider.FileExists(dbRestorePath))
            {
                return;
            }

            try
            {
                Logger.Info("Restoring Database");

                var dbPath = _appFolderInfo.GetDatabase();
                var dbBackupPath = dbPath + ".restore-backup";
                var databaseBackedUp = false;

                _diskProvider.DeleteFile(dbPath + "-shm");
                _diskProvider.DeleteFile(dbPath + "-wal");
                _diskProvider.DeleteFile(dbPath + "-journal");

                if (_diskProvider.FileExists(dbPath))
                {
                    _diskProvider.MoveFile(dbPath, dbBackupPath, true);
                    databaseBackedUp = true;
                }

                try
                {
                    _diskProvider.MoveFile(dbRestorePath, dbPath);

                    if (databaseBackedUp)
                    {
                        _diskProvider.DeleteFile(dbBackupPath);
                    }
                }
                catch
                {
                    if (databaseBackedUp)
                    {
                        _diskProvider.DeleteFile(dbPath);
                        _diskProvider.MoveFile(dbBackupPath, dbPath, true);
                    }

                    throw;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to restore database");
                throw;
            }
        }
    }
}
