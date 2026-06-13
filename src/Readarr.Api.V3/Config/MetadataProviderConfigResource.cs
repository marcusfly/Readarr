using NzbDrone.Core.Configuration;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Config
{
    public class MetadataProviderConfigResource : RestResource
    {
        public string MetadataProvider { get; set; }
        public string MetadataOpenLibrarySource { get; set; }
        public string MetadataRreadingGlassesSource { get; set; }
        public WriteAudioTagsType WriteAudioTags { get; set; }
        public bool ScrubAudioTags { get; set; }
        public WriteBookTagsType WriteBookTags { get; set; }
        public bool UpdateCovers { get; set; }
        public bool EmbedMetadata { get; set; }
    }

    public static class MetadataProviderConfigResourceMapper
    {
        public static MetadataProviderConfigResource ToResource(IConfigService model)
        {
            return new MetadataProviderConfigResource
            {
                MetadataProvider = model.MetadataProvider,
                MetadataOpenLibrarySource = model.MetadataOpenLibrarySource,
                MetadataRreadingGlassesSource = model.MetadataRreadingGlassesSource,
                WriteAudioTags = model.WriteAudioTags,
                ScrubAudioTags = model.ScrubAudioTags,
                WriteBookTags = model.WriteBookTags,
                UpdateCovers = model.UpdateCovers,
                EmbedMetadata = model.EmbedMetadata
            };
        }
    }
}
