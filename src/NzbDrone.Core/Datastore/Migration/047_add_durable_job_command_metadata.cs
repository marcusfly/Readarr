using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(047)]
    public class add_durable_job_command_metadata : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("JobAttempts")
                 .AddColumn("CommandBody").AsString().Nullable()
                 .AddColumn("CommandPriority").AsInt32().NotNullable().WithDefaultValue(0)
                 .AddColumn("CommandTrigger").AsInt32().NotNullable().WithDefaultValue(0);
        }
    }
}
