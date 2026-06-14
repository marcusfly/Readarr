using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.RreadingGlasses
{
    public class RgSearchResource
    {
        [JsonPropertyName("bookId")]
        public long BookId { get; set; }

        [JsonPropertyName("workId")]
        public long WorkId { get; set; }

        [JsonPropertyName("author")]
        public RgSearchAuthorResource Author { get; set; }
    }

    public class RgSearchAuthorResource
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    public class RgAuthorResource
    {
        public long ForeignId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string Url { get; set; }
        public int RatingCount { get; set; }
        public double AverageRating { get; set; }
        public List<RgWorkResource> Works { get; set; } = new List<RgWorkResource>();
        public List<RgSeriesResource> Series { get; set; } = new List<RgSeriesResource>();
    }

    public class RgWorkResource
    {
        public long ForeignId { get; set; }
        public string Title { get; set; }
        public string FullTitle { get; set; }
        public string ShortTitle { get; set; }
        public string Url { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string ReleaseDateRaw { get; set; }
        public List<string> Genres { get; set; } = new List<string>();
        public List<int> RelatedWorks { get; set; } = new List<int>();
        public List<RgBookResource> Books { get; set; } = new List<RgBookResource>();
        public List<RgSeriesResource> Series { get; set; } = new List<RgSeriesResource>();
        public List<RgAuthorResource> Authors { get; set; } = new List<RgAuthorResource>();
        public long BestBookId { get; set; }
        public int RatingCount { get; set; }
        public double AverageRating { get; set; }
    }

    public class RgBookResource
    {
        public long ForeignId { get; set; }
        public string Asin { get; set; }
        public string Description { get; set; }
        public string Isbn13 { get; set; }
        public string Title { get; set; }
        public string FullTitle { get; set; }
        public string ShortTitle { get; set; }
        public string Language { get; set; }
        public string Format { get; set; }
        public string EditionInformation { get; set; }
        public string Publisher { get; set; }
        public string ImageUrl { get; set; }
        public bool IsEbook { get; set; }
        public int? NumPages { get; set; }
        public int RatingCount { get; set; }
        public double AverageRating { get; set; }
        public string Url { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string ReleaseDateRaw { get; set; }
        public List<RgContributorResource> Contributors { get; set; } = new List<RgContributorResource>();
    }

    public class RgContributorResource
    {
        public long ForeignId { get; set; }
        public string Role { get; set; }
    }

    public class RgSeriesResource
    {
        public long ForeignId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<RgSeriesWorkLinkResource> LinkItems { get; set; } = new List<RgSeriesWorkLinkResource>();
    }

    public class RgSeriesWorkLinkResource
    {
        public long ForeignWorkId { get; set; }
        public string PositionInSeries { get; set; }
        public int SeriesPosition { get; set; }
        public bool Primary { get; set; }
    }

    public class RgRecentUpdatesResource
    {
        public bool Limited { get; set; }
        public List<long> Ids { get; set; } = new List<long>();
    }
}
