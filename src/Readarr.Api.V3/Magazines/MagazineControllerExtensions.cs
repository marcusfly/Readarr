using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Magazines;

namespace Readarr.Api.V3.Magazines
{
    public static class MagazineControllerExtensions
    {
        public static MagazineIssueResource ToResource(this MagazineIssue issue)
        {
            if (issue == null)
            {
                return null;
            }

            return new MagazineIssueResource
            {
                Id = issue.Id,
                MagazineId = issue.MagazineId,
                IssueYear = issue.IssueYear,
                IssueMonth = issue.IssueMonth,
                IssueDay = issue.IssueDay,
                Volume = issue.Volume,
                IssueNumber = issue.IssueNumber,
                ReleaseTitle = issue.ReleaseTitle,
                Monitored = issue.Monitored,
                HasFile = issue.IssueFiles?.Value != null && issue.IssueFiles.Value.Any(),
                Added = issue.Added,
                Quality = issue.IssueFiles?.Value?.FirstOrDefault()?.Quality
            };
        }

        public static MagazineIssue ToModel(this MagazineIssueResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new MagazineIssue
            {
                Id = resource.Id,
                MagazineId = resource.MagazineId,
                IssueYear = resource.IssueYear,
                IssueMonth = resource.IssueMonth,
                IssueDay = resource.IssueDay,
                Volume = resource.Volume,
                IssueNumber = resource.IssueNumber,
                ReleaseTitle = resource.ReleaseTitle,
                Monitored = resource.Monitored
            };
        }

        public static MagazineResource ToResource(this Magazine model, IEnumerable<MagazineIssue> issues)
        {
            if (model == null)
            {
                return null;
            }

            var issueList = issues?.ToList() ?? new List<MagazineIssue>();

            return new MagazineResource
            {
                Id = model.Id,
                Title = model.Title,
                CleanTitle = model.CleanTitle,
                Issn = model.Issn,
                WikidataId = model.WikidataId,
                Publisher = model.Publisher,
                Monitored = model.Monitored,
                Path = model.Path,
                RootFolderPath = model.RootFolderPath,
                QualityProfileId = model.QualityProfileId,
                MetadataProfileId = model.MetadataProfileId,
                Tags = model.Tags?.ToList(),
                Added = model.Added,
                AddOptions = model.AddOptions?.ToResource(),
                Statistics = new MagazineStatisticsResource
                {
                    IssueCount = issueList.Count,
                    MonitoredIssueCount = issueList.Count(x => x.Monitored),
                    IssueFileCount = issueList.Sum(x => x.IssueFiles?.Value?.Count ?? 0)
                }
            };
        }

        public static Magazine ToModel(this MagazineResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new Magazine
            {
                Id = resource.Id,
                Title = resource.Title,
                CleanTitle = resource.CleanTitle,
                Issn = resource.Issn,
                WikidataId = resource.WikidataId,
                Publisher = resource.Publisher,
                Monitored = resource.Monitored,
                Path = resource.Path,
                RootFolderPath = resource.RootFolderPath,
                QualityProfileId = resource.QualityProfileId,
                MetadataProfileId = resource.MetadataProfileId,
                Tags = resource.Tags?.ToHashSet(),
                Added = resource.Added,
                AddOptions = resource.AddOptions?.ToModel()
            };
        }

        public static Magazine ToModel(this MagazineResource resource, Magazine existing)
        {
            var updated = resource.ToModel();

            if (existing == null)
            {
                return updated;
            }

            existing.ApplyChanges(updated);
            return existing;
        }

        private static AddMagazineOptionsResource ToResource(this AddMagazineOptions model)
        {
            if (model == null)
            {
                return null;
            }

            return new AddMagazineOptionsResource
            {
                Monitor = model.Monitor,
                SearchForMissingIssues = model.SearchForMissingIssues
            };
        }

        private static AddMagazineOptions ToModel(this AddMagazineOptionsResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new AddMagazineOptions
            {
                Monitor = resource.Monitor,
                SearchForMissingIssues = resource.SearchForMissingIssues
            };
        }
    }
}
