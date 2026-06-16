using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Parser
{
    public interface IMagazineParsingService
    {
        RemoteMagazineIssue Map(ParsedMagazineIssueInfo parsedInfo, SearchCriteriaBase searchCriteria = null);
    }

    public class MagazineParsingService : IMagazineParsingService
    {
        private readonly IMagazineService _magazineService;
        private readonly IMagazineIssueService _magazineIssueService;

        public MagazineParsingService(IMagazineService magazineService, IMagazineIssueService magazineIssueService)
        {
            _magazineService = magazineService;
            _magazineIssueService = magazineIssueService;
        }

        public RemoteMagazineIssue Map(ParsedMagazineIssueInfo parsedInfo, SearchCriteriaBase searchCriteria = null)
        {
            if (parsedInfo == null)
            {
                return null;
            }

            Magazine magazine = null;
            MagazineIssue issue = null;

            if (searchCriteria is MagazineIssueSearchCriteria magazineSearchCriteria)
            {
                magazine = magazineSearchCriteria.Magazine;
                issue = magazineSearchCriteria.Issue;
            }

            if (magazine == null && !string.IsNullOrWhiteSpace(parsedInfo.NormalizedMagazineTitle))
            {
                magazine = _magazineService.FindByNormalizedTitle(parsedInfo.NormalizedMagazineTitle);
            }

            if (magazine != null && issue == null && parsedInfo.IssueYear > 0 && parsedInfo.IssueMonth > 0)
            {
                issue = _magazineIssueService.GetIssuesByMagazine(magazine.Id)
                                            .Find(x => x.IssueYear == parsedInfo.IssueYear &&
                                                       x.IssueMonth == parsedInfo.IssueMonth &&
                                                       x.IssueDay == parsedInfo.IssueDay);
            }

            return new RemoteMagazineIssue
            {
                Magazine = magazine,
                Issue = issue,
                ParsedMagazineIssueInfo = parsedInfo,
                ParsedBookInfo = ToParsedBookInfo(parsedInfo)
            };
        }

        private static ParsedBookInfo ToParsedBookInfo(ParsedMagazineIssueInfo parsedInfo)
        {
            var issueLabel = parsedInfo.IssueYear > 0 && parsedInfo.IssueMonth > 0
                ? $"{parsedInfo.IssueYear:D4}-{parsedInfo.IssueMonth:D2}{(parsedInfo.IssueDay.HasValue ? $"-{parsedInfo.IssueDay.Value:D2}" : string.Empty)}"
                : parsedInfo.ReleaseTitle;

            return new ParsedBookInfo
            {
                AuthorName = parsedInfo.MagazineTitle,
                BookTitle = issueLabel,
                Quality = parsedInfo.Quality,
                ReleaseTitle = parsedInfo.ReleaseTitle,
                Confidence = parsedInfo.Confidence
            };
        }
    }
}
