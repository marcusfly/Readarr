using System;
using System.Collections.Generic;
using NzbDrone.Core.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    public class MagazineResource : RestResource
    {
        public string Title { get; set; }
        public string CleanTitle { get; set; }
        public string Issn { get; set; }
        public string WikidataId { get; set; }
        public string Publisher { get; set; }
        public bool Monitored { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public int QualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }
        public List<int> Tags { get; set; }
        public DateTime Added { get; set; }
        public AddMagazineOptionsResource AddOptions { get; set; }
        public MagazineStatisticsResource Statistics { get; set; }
    }

    public class MagazineStatisticsResource
    {
        public int IssueCount { get; set; }
        public int IssueFileCount { get; set; }
        public int MonitoredIssueCount { get; set; }
    }

    public class AddMagazineOptionsResource
    {
        public MonitorTypes Monitor { get; set; }
        public bool SearchForMissingIssues { get; set; }
    }
}
