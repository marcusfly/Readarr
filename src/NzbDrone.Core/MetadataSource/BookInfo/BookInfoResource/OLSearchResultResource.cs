using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.BookInfo
{
    // GET /search.json?q=...
    public class OLSearchResponse
    {
        [JsonPropertyName("numFound")]
        public int NumFound { get; set; }

        [JsonPropertyName("start")]
        public int Start { get; set; }

        [JsonPropertyName("docs")]
        public List<OLSearchDoc> Docs { get; set; }
    }

    public class OLSearchDoc
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("author_name")]
        public List<string> AuthorName { get; set; }

        [JsonPropertyName("author_key")]
        public List<string> AuthorKey { get; set; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        [JsonPropertyName("isbn")]
        public List<string> Isbn { get; set; }

        [JsonPropertyName("cover_i")]
        public long? CoverId { get; set; }

        [JsonPropertyName("subject")]
        public List<string> Subject { get; set; }

        [JsonPropertyName("number_of_pages_median")]
        public int? NumberOfPagesMedian { get; set; }

        [JsonPropertyName("publisher")]
        public List<string> Publisher { get; set; }

        [JsonPropertyName("ratings_average")]
        public double? RatingsAverage { get; set; }

        [JsonPropertyName("ratings_count")]
        public int? RatingsCount { get; set; }
    }

    // GET /search/authors.json?q=...
    public class OLAuthorSearchResponse
    {
        [JsonPropertyName("numFound")]
        public int NumFound { get; set; }

        [JsonPropertyName("docs")]
        public List<OLAuthorSearchDoc> Docs { get; set; }
    }

    public class OLAuthorSearchDoc
    {
        // Note: author search returns bare OLIDs without path prefix e.g. "OL23919A"
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("alternate_names")]
        public List<string> AlternateNames { get; set; }

        [JsonPropertyName("birth_date")]
        public string BirthDate { get; set; }

        [JsonPropertyName("top_work")]
        public string TopWork { get; set; }

        [JsonPropertyName("work_count")]
        public int WorkCount { get; set; }

        [JsonPropertyName("top_subjects")]
        public List<string> TopSubjects { get; set; }
    }

    // GET /works/{OLID}/ratings.json
    public class OLRatingsResponse
    {
        [JsonPropertyName("summary")]
        public OLRatingsSummary Summary { get; set; }
    }

    public class OLRatingsSummary
    {
        [JsonPropertyName("average")]
        public double? Average { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
