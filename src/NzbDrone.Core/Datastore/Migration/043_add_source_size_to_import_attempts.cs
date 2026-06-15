using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(043)]
    public class add_source_size_to_import_attempts : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("ImportAttempts").AddColumn("SourceSize").AsInt64().NotNullable().WithDefaultValue(0);
        }
    }
}
