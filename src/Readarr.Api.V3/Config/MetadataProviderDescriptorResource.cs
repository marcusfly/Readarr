using System.Collections.Generic;

namespace Readarr.Api.V3.Config
{
    public class MetadataProviderDescriptorResource
    {
        public string ProviderKey { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }
        public int Capabilities { get; set; }
        public int ContentTypes { get; set; }
        public List<string> ContentTypeNames { get; set; }
    }
}
