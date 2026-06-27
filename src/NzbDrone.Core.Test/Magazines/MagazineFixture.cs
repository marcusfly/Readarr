using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Magazines;

namespace NzbDrone.Core.Test.Magazines
{
    [TestFixture]
    public class MagazineFixture
    {
        [Test]
        public void should_apply_mutable_changes()
        {
            var existing = new Magazine
            {
                Monitored = true,
                QualityProfileId = 1,
                MetadataProfileId = 2,
                Tags = new HashSet<int> { 1, 2 },
                AddOptions = new AddMagazineOptions
                {
                    SearchForMissingIssues = false
                }
            };

            var updated = new Magazine
            {
                Monitored = false,
                QualityProfileId = 4,
                MetadataProfileId = 7,
                Tags = new HashSet<int> { 9 },
                AddOptions = new AddMagazineOptions
                {
                    SearchForMissingIssues = true
                }
            };

            existing.ApplyChanges(updated);

            existing.Monitored.Should().BeFalse();
            existing.QualityProfileId.Should().Be(4);
            existing.MetadataProfileId.Should().Be(7);
            existing.Tags.Should().BeEquivalentTo(new[] { 9 });
            existing.AddOptions.SearchForMissingIssues.Should().BeTrue();
        }
    }
}
