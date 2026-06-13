using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(041)]
    public class add_job_attempts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.Table("JobAttempts")
                  .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                  .WithColumn("JobType").AsString().NotNullable()
                  .WithColumn("IdempotencyKey").AsString().NotNullable().Unique()
                  .WithColumn("State").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("QueuedAt").AsDateTime().NotNullable()
                  .WithColumn("StartedAt").AsDateTime().Nullable()
                  .WithColumn("CompletedAt").AsDateTime().Nullable()
                  .WithColumn("AttemptCount").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("LastError").AsString().Nullable()
                  .WithColumn("Progress").AsInt32().NotNullable().WithDefaultValue(0)
                  .WithColumn("LeaseToken").AsString().Nullable()
                  .WithColumn("CommandId").AsInt32().Nullable();
        }
    }
}
