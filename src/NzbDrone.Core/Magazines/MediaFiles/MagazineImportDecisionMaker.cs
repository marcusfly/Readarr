using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using NzbDrone.Common;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Magazines.MediaFiles
{
    public interface IMagazineImportDecisionMaker
    {
        List<MagazineImportDecision> GetImportDecisions(List<IFileInfo> files, Magazine magazine, ParsedMagazineIssueInfo folderInfo);
    }

    public class MagazineImportDecisionMaker : IMagazineImportDecisionMaker
    {
        private readonly IMagazineFilenameParser _filenameParser;
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineIssueFileService _magazineIssueFileService;

        public MagazineImportDecisionMaker(IMagazineFilenameParser filenameParser,
                                           IMagazineIssueService magazineIssueService,
                                           IMagazineIssueFileService magazineIssueFileService)
        {
            _filenameParser = filenameParser;
            _magazineIssueService = magazineIssueService;
            _magazineIssueFileService = magazineIssueFileService;
        }

        public List<MagazineImportDecision> GetImportDecisions(List<IFileInfo> files, Magazine magazine, ParsedMagazineIssueInfo folderInfo)
        {
            if (files == null || files.Count == 0 || magazine == null)
            {
                return new List<MagazineImportDecision>();
            }

            var existingIssueFiles = _magazineIssueFileService.GetFilesByMagazine(magazine.Id);
            var existingIssues = _magazineIssueService.GetIssuesByMagazine(magazine.Id);

            return files.Select(file =>
            {
                var parsed = _filenameParser.ParseFilename(file.Name, folderInfo?.MagazineTitle ?? magazine.Title);
                var issue = existingIssues.FirstOrDefault(x => x.IssueYear == parsed.IssueYear &&
                                                               x.IssueMonth == parsed.IssueMonth &&
                                                               x.IssueDay == parsed.IssueDay);

                var decision = new MagazineImportDecision
                {
                    LocalIssue = new LocalMagazineIssue
                    {
                        Magazine = magazine,
                        Issue = issue,
                        ParsedInfo = parsed,
                        Path = file,
                        Quality = parsed.Quality
                    }
                };

                if (parsed.Confidence <= 0)
                {
                    decision.Rejections.Add(new Rejection("ParseFailed"));
                    return decision;
                }

                if (existingIssueFiles.Any(x => PathEqualityComparer.Instance.Equals(x.Path, file.FullName)))
                {
                    decision.Rejections.Add(new Rejection("AlreadyImported"));
                    return decision;
                }

                if (issue != null)
                {
                    var issueFiles = existingIssueFiles.Where(x => x.MagazineIssueId == issue.Id).ToList();
                    if (issueFiles.Any(x => (int)(x.Quality?.Quality ?? Qualities.Quality.Unknown) >= (int)(parsed.Quality?.Quality ?? Qualities.Quality.Unknown)))
                    {
                        decision.Rejections.Add(new Rejection("ExistingFile"));
                    }
                }

                return decision;
            }).ToList();
        }
    }
}
