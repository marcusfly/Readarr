using System.Data;
using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(45)]
    public class add_magazine_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Create.TableForModel("Magazines")
                .WithColumn("CleanTitle").AsString().Indexed()
                .WithColumn("Title").AsString()
                .WithColumn("NormalizedTitle").AsString()
                .WithColumn("Aliases").AsString().NotNullable().WithDefaultValue("[]")
                .WithColumn("Issn").AsString().Nullable()
                .WithColumn("WikidataId").AsString().Nullable()
                .WithColumn("Publisher").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(false)
                .WithColumn("Path").AsString().Nullable().Indexed()
                .WithColumn("RootFolderPath").AsString().Nullable()
                .WithColumn("QualityProfileId").AsInt32().WithDefaultValue(1)
                .WithColumn("MetadataProfileId").AsInt32().WithDefaultValue(1)
                .WithColumn("Tags").AsString().Nullable()
                .WithColumn("Added").AsDateTime().Nullable()
                .WithColumn("LastInfoSync").AsDateTime().Nullable()
                .WithColumn("AddOptions").AsString().Nullable();

            Create.TableForModel("MagazineIssues")
                .WithColumn("MagazineId").AsInt32().NotNullable().Indexed().ForeignKey("Magazines", "Id").OnDelete(Rule.Cascade)
                .WithColumn("IssueYear").AsInt32().NotNullable()
                .WithColumn("IssueMonth").AsInt32().NotNullable()
                .WithColumn("IssueDay").AsInt32().Nullable()
                .WithColumn("Volume").AsString().Nullable()
                .WithColumn("IssueNumber").AsString().Nullable()
                .WithColumn("ReleaseTitle").AsString().Nullable()
                .WithColumn("Monitored").AsBoolean().WithDefaultValue(false)
                .WithColumn("Added").AsDateTime().Nullable()
                .WithColumn("LastSearchTime").AsDateTime().Nullable();

            Create.TableForModel("MagazineIssueFiles")
                .WithColumn("MagazineIssueId").AsInt32().NotNullable().Indexed().ForeignKey("MagazineIssues", "Id").OnDelete(Rule.Cascade)
                .WithColumn("MagazineId").AsInt32().NotNullable().Indexed().ForeignKey("Magazines", "Id").OnDelete(Rule.Cascade)
                .WithColumn("Path").AsString().NotNullable().Unique()
                .WithColumn("Size").AsInt64().WithDefaultValue(0)
                .WithColumn("DateAdded").AsDateTime().Nullable()
                .WithColumn("Quality").AsString().Nullable()
                .WithColumn("MediaInfo").AsString().Nullable();

            Create.TableForModel("MagazineRootFolders")
                .WithColumn("Name").AsString().Nullable()
                .WithColumn("Path").AsString().NotNullable().Unique()
                .WithColumn("DefaultQualityProfileId").AsInt32().WithDefaultValue(1)
                .WithColumn("DefaultMetadataProfileId").AsInt32().WithDefaultValue(1)
                .WithColumn("DefaultMonitorOption").AsInt32().WithDefaultValue(0)
                .WithColumn("DefaultTags").AsString().Nullable();

            Execute.Sql(
                @"CREATE UNIQUE INDEX ""IX_MagazineIssues_Identity""
                  ON ""MagazineIssues"" (""MagazineId"", ""IssueYear"", ""IssueMonth"", ""IssueDay"")");
        }
    }
}
