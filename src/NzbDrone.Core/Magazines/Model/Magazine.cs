using System;
using System.Collections.Generic;
using Equ;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Magazines
{
    public class Magazine : Entity<Magazine>
    {
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public string NormalizedTitle { get; set; }
        public List<string> Aliases { get; set; }
        public string Issn { get; set; }
        public string IssnL { get; set; }
        public string WikidataId { get; set; }
        public string Publisher { get; set; }
        public string Country { get; set; }
        public string Language { get; set; }
        public bool Monitored { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public int QualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }
        public HashSet<int> Tags { get; set; }
        public DateTime Added { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public AddMagazineOptions AddOptions { get; set; }

        // Lazy-loaded
        [MemberwiseEqualityIgnore]
        public LazyLoaded<QualityProfile> QualityProfile { get; set; }

        [MemberwiseEqualityIgnore]
        public LazyLoaded<MetadataProfile> MetadataProfile { get; set; }

        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<MagazineIssue>> Issues { get; set; }
    }
}
