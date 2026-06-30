using System;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using Microsoft.Data.Sqlite;
using NLog;
using NLog.Common;
using NLog.Config;
using NLog.Targets;
using Npgsql;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Instrumentation
{
    public class DatabaseTarget : TargetWithLayout, IHandle<ApplicationShutdownRequested>
    {
        private const string INSERT_COMMAND = "INSERT INTO \"Logs\" (\"Message\",\"Time\",\"Logger\",\"Exception\",\"ExceptionType\",\"Level\") " +
                                      "VALUES(@Message,@Time,@Logger,@Exception,@ExceptionType,@Level)";

        private readonly IConnectionStringFactory _connectionStringFactory;

        public DatabaseTarget(IConnectionStringFactory connectionStringFactory)
        {
            _connectionStringFactory = connectionStringFactory;
        }

        public void Register()
        {
            var target = new SlowRunningAsyncTargetWrapper(this) { TimeToSleepBetweenBatches = 500 };

            Rule = new LoggingRule("*", LogLevel.Info, target);

            LogManager.Configuration.AddTarget("DbLogger", target);
            LogManager.Configuration.LoggingRules.Add(Rule);
            LogManager.ConfigurationChanged += OnLogManagerOnConfigurationChanged;
            LogManager.ReconfigExistingLoggers();
        }

        public void UnRegister()
        {
            LogManager.ConfigurationChanged -= OnLogManagerOnConfigurationChanged;
            LogManager.Configuration.RemoveTarget("DbLogger");
            LogManager.Configuration.LoggingRules.Remove(Rule);
            LogManager.ReconfigExistingLoggers();
            Dispose();
        }

        private void OnLogManagerOnConfigurationChanged(object sender, LoggingConfigurationChangedEventArgs args)
        {
            if (args.ActivatedConfiguration != null)
            {
                Register();
            }
        }

        public LoggingRule Rule { get; set; }

        protected override void Write(LogEventInfo logEvent)
        {
            try
            {
                var log = new Log
                {
                    Time = logEvent.TimeStamp,
                    Logger = logEvent.LoggerName,
                    Level = logEvent.Level.Name
                };

                if (log.Logger.StartsWith("NzbDrone."))
                {
                    log.Logger = log.Logger.Remove(0, 9);
                }

                var message = logEvent.FormattedMessage;

                if (logEvent.Exception != null)
                {
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = logEvent.Exception.Message;
                    }
                    else
                    {
                        message += ": " + logEvent.Exception.Message;
                    }

                    log.Exception = CleanseLogMessage.Cleanse(logEvent.Exception.ToString());
                    log.ExceptionType = logEvent.Exception.GetType().ToString();
                }

                log.Message = CleanseLogMessage.Cleanse(message);

                var connectionInfo = _connectionStringFactory.LogDbConnection;

                if (connectionInfo.DatabaseType == DatabaseType.SQLite)
                {
                    WriteSqliteLog(log, connectionInfo.ConnectionString);
                }
                else
                {
                    WritePostgresLog(log, connectionInfo.ConnectionString);
                }
            }
            catch (SQLiteException ex)
            {
                InternalLogger.Error(ex, "Unable to save log event to database");
                throw;
            }
        }

        private void WritePostgresLog(Log log, string connectionString)
        {
            using (var connection =
                new NpgsqlConnection(connectionString))
            {
                connection.Open();
                using (var sqlCommand = connection.CreateCommand())
                {
                    sqlCommand.CommandText = INSERT_COMMAND;
                    sqlCommand.Parameters.Add(new NpgsqlParameter("Message", DbType.String) { Value = log.Message });
                    sqlCommand.Parameters.Add(new NpgsqlParameter("Time", DbType.DateTime) { Value = log.Time.ToUniversalTime() });
                    sqlCommand.Parameters.Add(new NpgsqlParameter("Logger", DbType.String) { Value = log.Logger });
                    sqlCommand.Parameters.Add(new NpgsqlParameter("Exception", DbType.String) { Value = log.Exception == null ? DBNull.Value : log.Exception });
                    sqlCommand.Parameters.Add(new NpgsqlParameter("ExceptionType", DbType.String) { Value = log.ExceptionType == null ? DBNull.Value : log.ExceptionType });
                    sqlCommand.Parameters.Add(new NpgsqlParameter("Level", DbType.String) { Value = log.Level });

                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        private void WriteSqliteLog(Log log, string connectionString)
        {
            using (var connection = OpenSqliteConnection(connectionString))
            {
                using (var sqlCommand = connection.CreateCommand())
                {
                    sqlCommand.CommandText = INSERT_COMMAND;
                    AddParameter(sqlCommand, "Message", DbType.String, log.Message);
                    AddParameter(sqlCommand, "Time", DbType.DateTime, log.Time.ToUniversalTime());
                    AddParameter(sqlCommand, "Logger", DbType.String, log.Logger);
                    AddParameter(sqlCommand, "Exception", DbType.String, log.Exception);
                    AddParameter(sqlCommand, "ExceptionType", DbType.String, log.ExceptionType);
                    AddParameter(sqlCommand, "Level", DbType.String, log.Level);
                    sqlCommand.ExecuteNonQuery();
                }
            }
        }

        private static DbConnection OpenSqliteConnection(string connectionString)
        {
            try
            {
                var connection = SQLiteFactory.Instance.CreateConnection();
                connection.ConnectionString = connectionString;
                connection.Open();
                return connection;
            }
            catch (TypeInitializationException ex) when (ex.InnerException is EntryPointNotFoundException or DllNotFoundException)
            {
                InternalLogger.Warn(ex, "Falling back to Microsoft.Data.Sqlite for database logging because System.Data.SQLite interop symbols are unavailable.");
                return OpenSqliteFallbackConnection(connectionString);
            }
            catch (DllNotFoundException ex)
            {
                InternalLogger.Warn(ex, "Falling back to Microsoft.Data.Sqlite for database logging because System.Data.SQLite interop is unavailable.");
                return OpenSqliteFallbackConnection(connectionString);
            }
            catch (EntryPointNotFoundException ex)
            {
                InternalLogger.Warn(ex, "Falling back to Microsoft.Data.Sqlite for database logging because System.Data.SQLite interop entry points are unavailable.");
                return OpenSqliteFallbackConnection(connectionString);
            }
        }

        private static DbConnection OpenSqliteFallbackConnection(string connectionString)
        {
            var builder = new SQLiteConnectionStringBuilder(connectionString);
            var sqliteBuilder = new SqliteConnectionStringBuilder { DataSource = builder.DataSource };

            var connection = new SqliteConnection(sqliteBuilder.ConnectionString);
            connection.Open();
            return connection;
        }

        private static void AddParameter(DbCommand command, string name, DbType dbType, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        public void Handle(ApplicationShutdownRequested message)
        {
            if (LogManager.Configuration != null && LogManager.Configuration.LoggingRules.Contains(Rule))
            {
                UnRegister();
            }
        }
    }
}
