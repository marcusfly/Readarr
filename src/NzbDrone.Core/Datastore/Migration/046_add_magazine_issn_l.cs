using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(046)]
    public class add_magazine_issn_l : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Alter.Table("Magazines")
                 .AddColumn("IssnL").AsString().Nullable()
                 .AddColumn("Country").AsString().Nullable()
                 .AddColumn("Language").AsString().Nullable();
        }
    }
}
