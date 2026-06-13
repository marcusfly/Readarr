using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    // GET /books/{OLID}.json  OR entry in /works/{OLID}/editions.json
    public class OLEditionResource
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; }

        [JsonPropertyName("publishers")]
        public List<string> Publishers { get; set; }

        [JsonPropertyName("publish_date")]
        public string PublishDate { get; set; }

        [JsonPropertyName("number_of_pages")]
        public int? NumberOfPages { get; set; }

        [JsonPropertyName("isbn_13")]
        public List<string> Isbn13 { get; set; }

        [JsonPropertyName("isbn_10")]
        public List<string> Isbn10 { get; set; }

        [JsonPropertyName("languages")]
        public List<OLKeyRef> Languages { get; set; }

        [JsonPropertyName("covers")]
        public List<long> Covers { get; set; }

        [JsonPropertyName("description")]
        public OLTextValue Description { get; set; }

        [JsonPropertyName("physical_format")]
        public string PhysicalFormat { get; set; }

        [JsonPropertyName("works")]
        public List<OLKeyRef> Works { get; set; }

        [JsonPropertyName("notes")]
        public OLTextValue Notes { get; set; }
    }

    // GET /works/{OLID}/editions.json
    public class OLEditionsResponse
    {
        [JsonPropertyName("entries")]
        public List<OLEditionResource> Entries { get; set; }

        [JsonPropertyName("links")]
        public OLPaginationLinks Links { get; set; }

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }
}
