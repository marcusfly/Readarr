using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(042)]
    public class add_import_attempts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.Table("ImportAttempts")
                  .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                  .WithColumn("SourcePath").AsString().NotNullable()
                  .WithColumn("DestinationPath").AsString().Nullable()
                  .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("StartedAt").AsCustom(IsPostgres ? "TIMESTAMPTZ" : "DATETIME").NotNullable()
                  .WithColumn("FinishedAt").AsCustom(IsPostgres ? "TIMESTAMPTZ" : "DATETIME").Nullable()
                  .WithColumn("IsDryRun").AsBoolean().NotNullable().WithDefaultValue(false)
                  .WithColumn("ErrorMessage").AsString().Nullable();

            Create.Index().OnTable("ImportAttempts").OnColumn("Status");
            Create.Index().OnTable("ImportAttempts").OnColumn("SourcePath");
        }
    }
}
