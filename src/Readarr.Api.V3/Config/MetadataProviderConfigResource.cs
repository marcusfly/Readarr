using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource.Contracts;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Config
{
    public class MetadataProviderConfigResource : RestResource
    {
        public string MetadataProvider { get; set; }
        public string MetadataSearchProviders { get; set; }
        public string MetadataOpenLibrarySource { get; set; }
        public string MetadataRreadingGlassesSource { get; set; }
        public bool DisableWikidataLookup { get; set; }
        public List<MetadataProviderDescriptorResource> AvailableMetadataProviders { get; set; }
        public WriteAudioTagsType WriteAudioTags { get; set; }
        public bool ScrubAudioTags { get; set; }
        public WriteBookTagsType WriteBookTags { get; set; }
        public bool UpdateCovers { get; set; }
        public bool EmbedMetadata { get; set; }
    }

    public static class MetadataProviderConfigResourceMapper
    {
        public static MetadataProviderConfigResource ToResource(IConfigService model, IEnumerable<IMetadataProviderV1> metadataProviders)
        {
            return new MetadataProviderConfigResource
            {
                MetadataProvider = model.MetadataProvider,
                MetadataSearchProviders = model.MetadataSearchProviders,
                MetadataOpenLibrarySource = model.MetadataOpenLibrarySource,
                MetadataRreadingGlassesSource = model.MetadataRreadingGlassesSource,
                DisableWikidataLookup = model.DisableWikidataLookup,
                AvailableMetadataProviders = metadataProviders?
                    .Where(x => x.Descriptor != null && x.Descriptor.ProviderKey.IsNotNullOrWhiteSpace())
                    .GroupBy(x => x.Descriptor.ProviderKey, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .OrderByDescending(x => x.Descriptor.Priority)
                    .ThenBy(x => x.Descriptor.ProviderKey, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new MetadataProviderDescriptorResource
                    {
                        ProviderKey = x.Descriptor.ProviderKey,
                        Name = x.Descriptor.DisplayName,
                        Priority = x.Descriptor.Priority,
                        Capabilities = (int)x.Descriptor.Capabilities,
                        ContentTypes = (int)x.Descriptor.ContentTypes,
                        ContentTypeNames = Enum.GetValues(x.Descriptor.ContentTypes.GetType())
                            .Cast<Enum>()
                            .Where(value => Convert.ToInt32(value) != 0 && x.Descriptor.ContentTypes.HasFlag(value))
                            .Select(value => value.ToString())
                            .ToList()
                    })
                    .ToList(),
                WriteAudioTags = model.WriteAudioTags,
                ScrubAudioTags = model.ScrubAudioTags,
                WriteBookTags = model.WriteBookTags,
                UpdateCovers = model.UpdateCovers,
                EmbedMetadata = model.EmbedMetadata
            };
        }
    }
}
