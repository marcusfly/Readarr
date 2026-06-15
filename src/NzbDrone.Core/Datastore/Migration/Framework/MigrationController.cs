using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace NzbDrone.Core.Datastore.Migration.Framework
{
    public interface IMigrationController
    {
        void Migrate(string connectionString, MigrationContext migrationContext, DatabaseType databaseType);
    }

    public class MigrationController : IMigrationController
    {
        private readonly Logger _logger;
        private readonly ILoggerProvider _migrationLoggerProvider;

        public MigrationController(Logger logger,
                                   ILoggerProvider migrationLoggerProvider)
        {
            _logger = logger;
            _migrationLoggerProvider = migrationLoggerProvider;
        }

        public void Migrate(string connectionString, MigrationContext migrationContext, DatabaseType databaseType)
        {
            var sw = Stopwatch.StartNew();

            _logger.Info("*** Migrating {0} ***", connectionString);
            var sanitizedConnectionString = SanitizeConnectionString(connectionString);

            ServiceProvider serviceProvider;

            var db = databaseType == DatabaseType.SQLite ? "sqlite" : "postgres";

            serviceProvider = new ServiceCollection()
                .AddLogging(b => b.AddNLog())
                .AddFluentMigratorCore()
                .Configure<RunnerOptions>(cfg => cfg.IncludeUntaggedMaintenances = true)
                .ConfigureRunner(
                    builder => builder
                    .AddPostgres()
                    .AddNzbDroneSQLite()
                    .WithGlobalConnectionString(sanitizedConnectionString)
                    .ScanIn(Assembly.GetExecutingAssembly()).For.All())
                .Configure<TypeFilterOptions>(opt => opt.Namespace = "NzbDrone.Core.Datastore.Migration")
                .Configure<ProcessorOptions>(opt =>
                {
                    opt.PreviewOnly = false;
                    opt.Timeout = TimeSpan.FromMinutes(5);
                })
                .Configure<SelectingProcessorAccessorOptions>(cfg =>
                {
                    cfg.ProcessorId = db;
                })
                .Configure<SelectingGeneratorAccessorOptions>(cfg =>
                {
                    cfg.GeneratorId = db;
                })
                .BuildServiceProvider();

            using (var scope = serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

                migrationContext.IsPostgres = databaseType == DatabaseType.PostgreSQL;
                MigrationContext.Current = migrationContext;

                if (migrationContext.DesiredVersion.HasValue)
                {
                    runner.MigrateUp(migrationContext.DesiredVersion.Value);
                }
                else
                {
                    runner.MigrateUp();
                }

                MigrationContext.Current = null;
            }

            sw.Stop();

            _logger.Debug("Took: {0}", sw.Elapsed);
        }

        private static string SanitizeConnectionString(string connectionString)
        {
            var parts = connectionString
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Where(p =>
                    {
                        var key = p.Split('=', 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        key = key?.Replace(" ", string.Empty);
                        return !string.Equals(key, "cachesize", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(key, "fullfsync", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(key, "datetimekind", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(key, "journalmode", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(key, "version", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(key, "busytimeout", StringComparison.OrdinalIgnoreCase);
                    });

            return string.Join(";", parts);
        }
    }
}
