using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class metadata_profile_ignored_to_listFixture : MigrationTest<metadata_profile_ignored_to_list>
    {
        [Test]
        public void should_convert_legacy_delimited_ignored_terms_to_json_array()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("MetadataProfiles").Row(new
                {
                    Name = "Legacy Delimited",
                    MinPopularity = 0.5,
                    SkipMissingDate = false,
                    SkipMissingIsbn = false,
                    SkipPartsAndSets = false,
                    SkipSeriesSecondary = false,
                    AllowedLanguages = "[]",
                    MinPages = 0,
                    Ignored = " epub, mobi ,pdf "
                });
            });

            var profile = db.Query<MetadataProfile033>("SELECT \"Id\", \"Ignored\" FROM \"MetadataProfiles\" WHERE \"Name\" = 'Legacy Delimited'").Single();

            profile.Ignored.Should().Be(new[] { "epub", "mobi", "pdf" }.ToJson());
        }

        [Test]
        public void should_trim_and_deduplicate_legacy_ignored_terms()
        {
            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("MetadataProfiles").Row(new
                {
                    Name = "Legacy Duplicate Terms",
                    MinPopularity = 1.0,
                    SkipMissingDate = true,
                    SkipMissingIsbn = false,
                    SkipPartsAndSets = true,
                    SkipSeriesSecondary = false,
                    AllowedLanguages = "[]",
                    MinPages = 10,
                    Ignored = "azw3, pdf, azw3, , pdf, mobi "
                });
            });

            var profile = db.Query<MetadataProfile033>("SELECT \"Id\", \"Ignored\" FROM \"MetadataProfiles\" WHERE \"Name\" = 'Legacy Duplicate Terms'").Single();

            profile.Ignored.Should().Be(new[] { "azw3", "pdf", "mobi" }.ToJson());
        }

        [Test]
        public void should_leave_existing_json_ignored_terms_untouched()
        {
            var existingJson = new[] { "cbz", "cbr" }.ToJson();

            var db = WithMigrationTestDb(c =>
            {
                c.Insert.IntoTable("MetadataProfiles").Row(new
                {
                    Name = "Already Json",
                    MinPopularity = 0.0,
                    SkipMissingDate = false,
                    SkipMissingIsbn = true,
                    SkipPartsAndSets = false,
                    SkipSeriesSecondary = true,
                    AllowedLanguages = "[]",
                    MinPages = 5,
                    Ignored = existingJson
                });
            });

            var profile = db.Query<MetadataProfile033>("SELECT \"Id\", \"Ignored\" FROM \"MetadataProfiles\" WHERE \"Name\" = 'Already Json'").Single();

            profile.Ignored.Should().Be(existingJson);
        }
    }

    public class MetadataProfile033
    {
        public int Id { get; set; }
        public string Ignored { get; set; }
    }
}
