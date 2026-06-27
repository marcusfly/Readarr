using System.Collections.Generic;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Magazines.Metadata
{
    internal class MagazineAuthorityRecord
    {
        public string RawTitle { get; set; }
        public string CanonicalTitle { get; set; }
        public string NormalizedTitle { get; set; }
        public string WikidataId { get; set; }
        public string Issn { get; set; }
        public string IssnL { get; set; }
        public string Country { get; set; }
        public string Language { get; set; }
        public List<string> Aliases { get; set; }
        public string Publisher { get; set; }

        public MagazineAuthorityResult ToResult(string fallbackTitle = null)
        {
            var canonicalTitle = CanonicalTitle.IsNotNullOrWhiteSpace()
                ? CanonicalTitle.Trim()
                : RawTitle.IsNotNullOrWhiteSpace()
                    ? RawTitle.Trim()
                    : fallbackTitle?.Trim();

            if (canonicalTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            return new MagazineAuthorityResult
            {
                CanonicalTitle = canonicalTitle,
                NormalizedTitle = NormalizedTitle.IsNotNullOrWhiteSpace()
                    ? NormalizedTitle.Trim()
                    : MagazineTitleNormalizer.Normalize(canonicalTitle),
                WikidataId = WikidataId,
                Issn = Issn,
                IssnL = IssnL,
                Country = Country,
                Language = Language,
                Aliases = Aliases,
                Publisher = Publisher
            };
        }
    }
}
