using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Qualities;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    public class MagazineImportResource : RestResource
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public int MagazineId { get; set; }
        public int MagazineIssueId { get; set; }
        public int IssueYear { get; set; }
        public int IssueMonth { get; set; }
        public int? IssueDay { get; set; }
        public string Volume { get; set; }
        public string IssueNumber { get; set; }
        public string ReleaseTitle { get; set; }
        public QualityModel Quality { get; set; }
        public IEnumerable<Rejection> Rejections { get; set; }
    }

    public class MagazineImportUpdateResource : RestResource
    {
        public string Path { get; set; }
        public int MagazineIssueId { get; set; }
        public QualityModel Quality { get; set; }
    }

    public static class MagazineImportResourceMapper
    {
        public static MagazineImportResource ToResource(this NzbDrone.Core.Magazines.Services.MagazineImportItem item)
        {
            if (item == null)
            {
                return null;
            }

            return new MagazineImportResource
            {
                Id = item.Id,
                Path = item.Path,
                Name = item.Name,
                Size = item.Size,
                MagazineId = item.MagazineId,
                MagazineIssueId = item.MagazineIssueId,
                IssueYear = item.IssueYear,
                IssueMonth = item.IssueMonth,
                IssueDay = item.IssueDay,
                Volume = item.Volume,
                IssueNumber = item.IssueNumber,
                ReleaseTitle = item.ReleaseTitle,
                Quality = item.Quality,
                Rejections = item.Rejections
            };
        }

        public static List<MagazineImportResource> ToResource(this IEnumerable<NzbDrone.Core.Magazines.Services.MagazineImportItem> items)
        {
            return items == null ? new List<MagazineImportResource>() : new List<MagazineImportResource>(items.Select(ToResource));
        }
    }
}
