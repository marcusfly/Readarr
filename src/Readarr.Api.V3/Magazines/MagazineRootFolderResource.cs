using System.Collections.Generic;
using NzbDrone.Core.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    public class MagazineRootFolderResource : RestResource
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public int DefaultQualityProfileId { get; set; }
        public int DefaultMetadataProfileId { get; set; }
        public MonitorTypes DefaultMonitorOption { get; set; }
        public HashSet<int> DefaultTags { get; set; }
        public bool Accessible { get; set; }
        public long? FreeSpace { get; set; }
        public long? TotalSpace { get; set; }
    }

    public static class MagazineRootFolderResourceMapper
    {
        public static MagazineRootFolderResource ToResource(this NzbDrone.Core.Magazines.MagazineRootFolder model)
        {
            if (model == null)
            {
                return null;
            }

            return new MagazineRootFolderResource
            {
                Id = model.Id,
                Name = model.Name,
                Path = model.Path,
                DefaultQualityProfileId = model.DefaultQualityProfileId,
                DefaultMetadataProfileId = model.DefaultMetadataProfileId,
                DefaultMonitorOption = model.DefaultMonitorOption,
                DefaultTags = model.DefaultTags,
                Accessible = model.Accessible,
                FreeSpace = model.FreeSpace,
                TotalSpace = model.TotalSpace
            };
        }

        public static NzbDrone.Core.Magazines.MagazineRootFolder ToModel(this MagazineRootFolderResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new NzbDrone.Core.Magazines.MagazineRootFolder
            {
                Id = resource.Id,
                Name = resource.Name,
                Path = resource.Path,
                DefaultQualityProfileId = resource.DefaultQualityProfileId,
                DefaultMetadataProfileId = resource.DefaultMetadataProfileId,
                DefaultMonitorOption = resource.DefaultMonitorOption,
                DefaultTags = resource.DefaultTags ?? new HashSet<int>()
            };
        }
    }
}
