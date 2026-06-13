using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    // GET /works/{OLID}.json
    public class OLWorkResource
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("description")]
        public OLTextValue Description { get; set; }

        [JsonPropertyName("covers")]
        public List<long> Covers { get; set; }

        [JsonPropertyName("subjects")]
        public List<string> Subjects { get; set; }

        [JsonPropertyName("authors")]
        public List<OLWorkAuthorRef> Authors { get; set; }

        [JsonPropertyName("first_publish_date")]
        public string FirstPublishDate { get; set; }

        [JsonPropertyName("links")]
        public List<OLLinkResource> Links { get; set; }

        [JsonPropertyName("series")]
        public List<string> SeriesList { get; set; }
    }

    public class OLWorkAuthorRef
    {
        [JsonPropertyName("author")]
        public OLKeyRef Author { get; set; }

        [JsonPropertyName("type")]
        public OLKeyRef Type { get; set; }
    }

    public class OLKeyRef
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }
    }

    public class OLPaginationLinks
    {
        [JsonPropertyName("self")]
        public string Self { get; set; }

        [JsonPropertyName("next")]
        public string Next { get; set; }
    }
}
