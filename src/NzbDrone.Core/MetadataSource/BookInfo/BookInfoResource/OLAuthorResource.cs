using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    // GET /authors/{OLID}.json
    public class OLAuthorResource
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("personal_name")]
        public string PersonalName { get; set; }

        [JsonPropertyName("bio")]
        public OLTextValue Bio { get; set; }

        [JsonPropertyName("birth_date")]
        public string BirthDate { get; set; }

        [JsonPropertyName("death_date")]
        public string DeathDate { get; set; }

        [JsonPropertyName("alternate_names")]
        public List<string> AlternateNames { get; set; }

        [JsonPropertyName("photos")]
        public List<long> Photos { get; set; }

        [JsonPropertyName("wikipedia")]
        public string Wikipedia { get; set; }

        [JsonPropertyName("links")]
        public List<OLLinkResource> Links { get; set; }

        [JsonPropertyName("remote_ids")]
        public OLRemoteIds RemoteIds { get; set; }
    }

    public class OLLinkResource
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; }
    }

    public class OLRemoteIds
    {
        [JsonPropertyName("goodreads")]
        public string Goodreads { get; set; }

        [JsonPropertyName("isni")]
        public string Isni { get; set; }

        [JsonPropertyName("wikidata")]
        public string Wikidata { get; set; }
    }

    // GET /authors/{OLID}/works.json
    public class OLAuthorWorksResource
    {
        [JsonPropertyName("entries")]
        public List<OLWorkResource> Entries { get; set; }

        [JsonPropertyName("links")]
        public OLPaginationLinks PaginationLinks { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }
}
