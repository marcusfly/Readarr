using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Download.Aggregation;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
    public interface IMakeDownloadDecision
    {
        List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false);
        List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase);
        List<DownloadDecision> GetMagazineSearchDecision(List<ReleaseInfo> reports, MagazineIssueSearchCriteria searchCriteria);
    }

    public class DownloadDecisionMaker : IMakeDownloadDecision
    {
        private readonly IEnumerable<IDecisionEngineSpecification> _specifications;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IParsingService _parsingService;
        private readonly IMagazineParsingService _magazineParsingService;
        private readonly IMagazineFilenameParser _magazineFilenameParser;
        private readonly IRemoteBookAggregationService _aggregationService;
        private readonly Logger _logger;

        public DownloadDecisionMaker(IEnumerable<IDecisionEngineSpecification> specifications,
            IParsingService parsingService,
            IMagazineParsingService magazineParsingService,
            IMagazineFilenameParser magazineFilenameParser,
            ICustomFormatCalculationService formatService,
            IRemoteBookAggregationService aggregationService,
            Logger logger)
        {
            _specifications = specifications;
            _parsingService = parsingService;
            _magazineParsingService = magazineParsingService;
            _magazineFilenameParser = magazineFilenameParser;
            _formatCalculator = formatService;
            _aggregationService = aggregationService;
            _logger = logger;
        }

        public List<DownloadDecision> GetRssDecision(List<ReleaseInfo> reports, bool pushedRelease = false)
        {
            return GetBookDecisions(reports).ToList();
        }

        public List<DownloadDecision> GetSearchDecision(List<ReleaseInfo> reports, SearchCriteriaBase searchCriteriaBase)
        {
            return GetBookDecisions(reports, false, searchCriteriaBase).ToList();
        }

        public List<DownloadDecision> GetMagazineSearchDecision(List<ReleaseInfo> reports, MagazineIssueSearchCriteria searchCriteria)
        {
            if (searchCriteria == null)
            {
                return new List<DownloadDecision>();
            }

            var result = new List<DownloadDecision>();

            foreach (var report in reports)
            {
                var parsed = _magazineFilenameParser.ParseFilename(report.Title, searchCriteria.MagazineTitle);
                var remoteIssue = _magazineParsingService.Map(parsed, searchCriteria);

                if (remoteIssue == null)
                {
                    continue;
                }

                remoteIssue.Release = report;
                remoteIssue.ReleaseSource = searchCriteria.InteractiveSearch
                    ? ReleaseSourceType.InteractiveSearch
                    : searchCriteria.UserInvokedSearch
                        ? ReleaseSourceType.UserInvokedSearch
                        : ReleaseSourceType.Search;
                remoteIssue.DownloadAllowed = remoteIssue.Magazine != null && remoteIssue.Issue != null;

                DownloadDecision decision;

                if (remoteIssue.Magazine == null)
                {
                    decision = new DownloadDecision(remoteIssue, new Rejection("Unknown magazine"));
                }
                else if (remoteIssue.Issue == null)
                {
                    decision = new DownloadDecision(remoteIssue, new Rejection("Unable to match magazine issue from release name"));
                }
                else
                {
                    decision = GetMagazineDecisionForReport(remoteIssue, searchCriteria);
                }

                result.Add(decision);
            }

            return result;
        }

        private IEnumerable<DownloadDecision> GetBookDecisions(List<ReleaseInfo> reports, bool pushedRelease = false, SearchCriteriaBase searchCriteria = null)
        {
            if (reports.Any())
            {
                _logger.ProgressInfo("Processing {0} releases", reports.Count);
            }
            else
            {
                _logger.ProgressInfo("No results found");
            }

            var reportNumber = 1;

            foreach (var report in reports)
            {
                DownloadDecision decision = null;
                _logger.ProgressTrace("Processing release {0}/{1}", reportNumber, reports.Count);
                _logger.Debug("Processing release '{0}' from '{1}'", report.Title, report.Indexer);

                try
                {
                    var parsedBookInfo = Parser.Parser.ParseBookTitle(report.Title);

                    if (parsedBookInfo == null)
                    {
                        if (searchCriteria != null)
                        {
                            parsedBookInfo = Parser.Parser.ParseBookTitleWithSearchCriteria(report.Title,
                                                                                              searchCriteria.Author,
                                                                                              searchCriteria.Books);
                        }
                        else
                        {
                            // try parsing fuzzy
                            parsedBookInfo = _parsingService.ParseBookTitleFuzzy(report.Title);
                        }
                    }

                    if (parsedBookInfo != null)
                    {
                        _logger.Debug("Parsed release '{0}' with confidence {1:F2}", report.Title, parsedBookInfo.Confidence);

                        if (string.IsNullOrWhiteSpace(parsedBookInfo.RejectionReason) && parsedBookInfo.Confidence < 0.5)
                        {
                            parsedBookInfo.RejectionReason = $"Low parse confidence ({parsedBookInfo.Confidence:F2}) — ambiguous or incomplete title";
                        }
                    }

                    if (parsedBookInfo != null && !parsedBookInfo.AuthorName.IsNullOrWhiteSpace())
                    {
                        var remoteBook = _parsingService.Map(parsedBookInfo, searchCriteria);
                        remoteBook.Release = report;

                        _aggregationService.Augment(remoteBook);

                        // try parsing again using the search criteria, in case it parsed but parsed incorrectly
                        if ((remoteBook.Author == null || remoteBook.Books.Empty()) && searchCriteria != null)
                        {
                            _logger.Debug("Author/Book null for {0}, reparsing with search criteria", report.Title);
                            var parsedBookInfoWithCriteria = Parser.Parser.ParseBookTitleWithSearchCriteria(report.Title,
                                                                                                                searchCriteria.Author,
                                                                                                                searchCriteria.Books);

                            if (parsedBookInfoWithCriteria != null && parsedBookInfoWithCriteria.AuthorName.IsNotNullOrWhiteSpace())
                            {
                                remoteBook = _parsingService.Map(parsedBookInfoWithCriteria, searchCriteria);
                            }
                        }

                        remoteBook.Release = report;

                        // parse quality again with title and category if unknown
                        if (remoteBook.ParsedBookInfo.Quality.Quality == Quality.Unknown)
                        {
                            remoteBook.ParsedBookInfo.Quality = QualityParser.ParseQuality(report.Title, null, report.Categories);
                        }

                        if (remoteBook.Author == null)
                        {
                            decision = new DownloadDecision(remoteBook, new Rejection("Unknown Author"));

                            // shove in the searched author in case of forced download in interactive search
                            if (searchCriteria != null)
                            {
                                remoteBook.Author = searchCriteria.Author;
                                remoteBook.Books = searchCriteria.Books;
                            }
                        }
                        else if (remoteBook.Books.Empty())
                        {
                            decision = new DownloadDecision(remoteBook, new Rejection("Unable to parse books from release name"));
                            if (searchCriteria != null)
                            {
                                remoteBook.Books = searchCriteria.Books;
                            }
                        }
                        else
                        {
                            _aggregationService.Augment(remoteBook);

                            remoteBook.CustomFormats = _formatCalculator.ParseCustomFormat(remoteBook, remoteBook.Release.Size);
                            remoteBook.CustomFormatScore = remoteBook?.Author?.QualityProfile?.Value.CalculateCustomFormatScore(remoteBook.CustomFormats) ?? 0;

                            remoteBook.DownloadAllowed = remoteBook.Books.Any();
                            decision = GetDecisionForReport(remoteBook, searchCriteria);
                        }
                    }

                    if (searchCriteria != null)
                    {
                        if (parsedBookInfo == null)
                        {
                            parsedBookInfo = new ParsedBookInfo
                            {
                                Quality = QualityParser.ParseQuality(report.Title, null, report.Categories)
                            };
                        }

                        if (parsedBookInfo.AuthorName.IsNullOrWhiteSpace())
                        {
                            parsedBookInfo.RejectionReason = "Unable to parse release from title";
                            var remoteBook = new RemoteBook
                            {
                                Release = report,
                                ParsedBookInfo = parsedBookInfo
                            };

                            decision = new DownloadDecision(remoteBook, new Rejection("Unable to parse release"));
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't process release.");

                    var parsed = new ParsedBookInfo
                    {
                        Quality = QualityParser.ParseQuality(report.Title, null, report.Categories),
                        RejectionReason = "Unexpected error during parsing/mapping"
                    };
                    var remoteBook = new RemoteBook { Release = report, ParsedBookInfo = parsed };
                    decision = new DownloadDecision(remoteBook, new Rejection("Unexpected error processing release"));
                }

                reportNumber++;

                if (decision != null)
                {
                    var source = pushedRelease ? ReleaseSourceType.ReleasePush : ReleaseSourceType.Rss;

                    if (searchCriteria != null)
                    {
                        if (searchCriteria.InteractiveSearch)
                        {
                            source = ReleaseSourceType.InteractiveSearch;
                        }
                        else if (searchCriteria.UserInvokedSearch)
                        {
                            source = ReleaseSourceType.UserInvokedSearch;
                        }
                        else
                        {
                            source = ReleaseSourceType.Search;
                        }
                    }

                    decision.RemoteBook.ReleaseSource = source;

                    if (decision.Rejections.Any())
                    {
                        _logger.Debug("Release rejected for the following reasons: {0}", string.Join(", ", decision.Rejections));
                    }
                    else
                    {
                        _logger.Debug("Release accepted");
                    }

                    yield return decision;
                }
            }
        }

        private DownloadDecision GetDecisionForReport(RemoteBook remoteBook, SearchCriteriaBase searchCriteria = null)
        {
            var reasons = new Rejection[0];

            foreach (var specifications in _specifications.GroupBy(v => v.Priority).OrderBy(v => v.Key))
            {
                reasons = specifications.Select(c => EvaluateSpec(c, remoteBook, searchCriteria))
                                                        .Where(c => c != null)
                                                        .ToArray();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteBook, reasons.ToArray());
        }

        private DownloadDecision GetMagazineDecisionForReport(RemoteMagazineIssue remoteIssue, MagazineIssueSearchCriteria searchCriteria)
        {
            var selectedSpecifications = _specifications
                .Where(spec => spec is Specifications.BlockedIndexerSpecification ||
                               spec is Specifications.MaximumSizeSpecification ||
                               spec is Specifications.MinimumAgeSpecification ||
                               spec is Specifications.NotSampleSpecification ||
                               spec is Specifications.RawDiskSpecification ||
                               spec is Specifications.RssSync.MonitoredMagazineIssueSpecification)
                .GroupBy(spec => spec.Priority)
                .OrderBy(spec => spec.Key);

            var reasons = new List<Rejection>();

            foreach (var specificationGroup in selectedSpecifications)
            {
                reasons = specificationGroup
                    .Select(spec => EvaluateSpec(spec, remoteIssue, searchCriteria))
                    .Where(rejection => rejection != null)
                    .ToList();

                if (reasons.Any())
                {
                    break;
                }
            }

            return new DownloadDecision(remoteIssue, reasons.ToArray());
        }

        private Rejection EvaluateSpec(IDecisionEngineSpecification spec, RemoteBook remoteBook, SearchCriteriaBase searchCriteriaBase = null)
        {
            try
            {
                var result = spec.IsSatisfiedBy(remoteBook, searchCriteriaBase);

                if (!result.Accepted)
                {
                    return new Rejection(result.Reason, spec.Type);
                }
            }
            catch (NotImplementedException)
            {
                _logger.Trace("Spec " + spec.GetType().Name + " not implemented.");
            }
            catch (Exception e)
            {
                e.Data.Add("report", remoteBook.Release.ToJson());
                e.Data.Add("parsed", remoteBook.ParsedBookInfo.ToJson());
                _logger.Error(e, "Couldn't evaluate decision on {0}", remoteBook.Release.Title);
                return new Rejection($"{spec.GetType().Name}: {e.Message}");
            }

            return null;
        }
    }
}
