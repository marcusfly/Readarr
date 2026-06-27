using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NzbDrone.Core.Magazines.Metadata
{
    public class MagazineAuthorityResult
    {
        public string CanonicalTitle { get; set; }
        public string NormalizedTitle { get; set; }
        public string WikidataId { get; set; }
        public string Issn { get; set; }
        public string IssnL { get; set; }
        public string Country { get; set; }
        public string Language { get; set; }
        public List<string> Aliases { get; set; }
        public string Publisher { get; set; }
        public string ImageUrl { get; set; }
        public string OfficialWebsite { get; set; }
        public string LogoUrl { get; set; }
        public string Description { get; set; }
    }

    public interface IMagazineTitleAuthorityProvider
    {
        Task<MagazineAuthorityResult> LookupByTitleAsync(string rawTitle, CancellationToken ct = default);
    }
}
