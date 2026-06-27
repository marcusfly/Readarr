using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Npgsql;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Datastore
{
    public interface IConnectionStringFactory
    {
        DatabaseConnectionInfo MainDbConnection { get; }
        DatabaseConnectionInfo LogDbConnection { get; }
        DatabaseConnectionInfo CacheDbConnection { get; }
        string GetDatabasePath(string connectionString);
    }

    public class ConnectionStringFactory : IConnectionStringFactory
    {
        private readonly IConfigFileProvider _configFileProvider;

        public ConnectionStringFactory(IAppFolderInfo appFolderInfo, IConfigFileProvider configFileProvider)
        {
            _configFileProvider = configFileProvider;

            MainDbConnection = _configFileProvider.PostgresHost.IsNotNullOrWhiteSpace() ? GetPostgresConnectionString(_configFileProvider.PostgresMainDb) :
                GetConnectionString(appFolderInfo.GetDatabase());

            LogDbConnection = _configFileProvider.PostgresHost.IsNotNullOrWhiteSpace() ? GetPostgresConnectionString(_configFileProvider.PostgresLogDb) :
                GetConnectionString(appFolderInfo.GetLogDatabase());

            CacheDbConnection = _configFileProvider.PostgresHost.IsNotNullOrWhiteSpace() ? GetPostgresConnectionString(_configFileProvider.PostgresCacheDb) :
                GetConnectionString(appFolderInfo.GetCacheDatabase());
        }

        public DatabaseConnectionInfo MainDbConnection { get; private set; }
        public DatabaseConnectionInfo LogDbConnection { get; private set; }
        public DatabaseConnectionInfo CacheDbConnection { get; private set; }

        public string GetDatabasePath(string connectionString)
        {
            var connectionBuilder = new SQLiteConnectionStringBuilder(connectionString);

            return connectionBuilder.DataSource;
        }

        private static DatabaseConnectionInfo GetConnectionString(string dbPath)
        {
            var useTruncateJournal = ShouldUseTruncateJournal(dbPath);

            var connectionBuilder = new SQLiteConnectionStringBuilder
            {
                DataSource = dbPath,
                CacheSize = -20000,
                DateTimeKind = DateTimeKind.Utc,
                JournalMode = useTruncateJournal ? SQLiteJournalModeEnum.Truncate : SQLiteJournalModeEnum.Wal,
                Pooling = true,
                Version = 3,
                BusyTimeout = 100
            };

            if (OsInfo.IsOsx)
            {
                connectionBuilder.Add("Full FSync", true);
            }

            return new DatabaseConnectionInfo(DatabaseType.SQLite, connectionBuilder.ConnectionString);
        }

        private static bool ShouldUseTruncateJournal(string dbPath)
        {
            if (OsInfo.IsOsx)
            {
                return true;
            }

            if (!OsInfo.IsLinux || string.IsNullOrWhiteSpace(dbPath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(dbPath);
                var mountEntry = File.ReadLines("/proc/mounts")
                    .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    .Where(parts => parts.Length >= 3)
                    .OrderByDescending(parts => parts[1].Length)
                    .FirstOrDefault(parts => fullPath.StartsWith(parts[1], StringComparison.Ordinal));

                if (mountEntry == null)
                {
                    return false;
                }

                var source = mountEntry[0];
                var fileSystemType = mountEntry[2];

                return source.StartsWith("/run/host_mark/", StringComparison.OrdinalIgnoreCase) ||
                       fileSystemType.Equals("fakeowner", StringComparison.OrdinalIgnoreCase) ||
                       fileSystemType.Equals("osxfs", StringComparison.OrdinalIgnoreCase) ||
                       fileSystemType.Equals("virtiofs", StringComparison.OrdinalIgnoreCase) ||
                       fileSystemType.Equals("fuse.osxfs", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private DatabaseConnectionInfo GetPostgresConnectionString(string dbName)
        {
            var connectionBuilder = new NpgsqlConnectionStringBuilder
            {
                Database = dbName,
                Host = _configFileProvider.PostgresHost,
                Username = _configFileProvider.PostgresUser,
                Password = _configFileProvider.PostgresPassword,
                Port = _configFileProvider.PostgresPort,
                Enlist = false
            };

            return new DatabaseConnectionInfo(DatabaseType.PostgreSQL, connectionBuilder.ConnectionString);
        }
    }
}
