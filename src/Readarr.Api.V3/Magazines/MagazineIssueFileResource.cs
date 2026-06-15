using NzbDrone.Core.Qualities;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    public class MagazineIssueFileResource : RestResource
    {
        public int MagazineIssueId { get; set; }
        public int MagazineId { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public QualityModel Quality { get; set; }
    }

    public static class MagazineIssueFileResourceExtensions
    {
        public static MagazineIssueFileResource ToResource(this NzbDrone.Core.Magazines.MagazineIssueFile file)
        {
            return new MagazineIssueFileResource
            {
                Id = file.Id,
                MagazineIssueId = file.MagazineIssueId,
                MagazineId = file.MagazineId,
                Path = file.Path,
                Size = file.Size,
                Quality = file.Quality
            };
        }
    }
}
