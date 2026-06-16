using System.Text.RegularExpressions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Magazines.Parser
{
    public interface IMagazineFilenameParser
    {
        ParsedMagazineIssueInfo ParseFilename(string filename, string magazineFolderName);
        ParsedMagazineIssueInfo ParseFolderName(string folderName);
    }

    public class MagazineFilenameParser : IMagazineFilenameParser
    {
        private static readonly Regex SeparatorRegex = new Regex(@"[\s]*(?:\x2D|\x2013|\x2014)[\s]*", RegexOptions.Compiled);
        private static readonly Regex FullDateRegex = new Regex(@"(\d{4})-(\d{1,2})(?:-(\d{1,2}))?", RegexOptions.Compiled);
        private static readonly Regex FallbackDateRegex = new Regex(@"(\d{4})[\W_]?(\d{2})", RegexOptions.Compiled);

        public ParsedMagazineIssueInfo ParseFilename(string filename, string magazineFolderName)
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(filename) ?? string.Empty;
            var parsed = ParseFolderName(magazineFolderName);
            parsed.ReleaseTitle = baseName;
            parsed.Quality = MagazineFormatDetector.DetectQuality(filename);

            var separatorMatch = SeparatorRegex.Match(baseName);
            if (separatorMatch.Success)
            {
                var rightSide = baseName.Substring(separatorMatch.Index + separatorMatch.Length);
                var dateMatch = FullDateRegex.Match(rightSide);

                if (dateMatch.Success)
                {
                    ApplyDate(parsed, dateMatch, true);
                    return parsed;
                }
            }

            var fullDateMatch = FullDateRegex.Match(baseName);
            if (fullDateMatch.Success)
            {
                ApplyDate(parsed, fullDateMatch, true);
                return parsed;
            }

            var fallbackMatch = FallbackDateRegex.Match(baseName);
            if (fallbackMatch.Success)
            {
                ApplyDate(parsed, fallbackMatch, false);
                return parsed;
            }

            parsed.Confidence = 0f;
            return parsed;
        }

        public ParsedMagazineIssueInfo ParseFolderName(string folderName)
        {
            var title = folderName ?? string.Empty;

            return new ParsedMagazineIssueInfo
            {
                MagazineTitle = title,
                NormalizedMagazineTitle = MagazineTitleNormalizer.Normalize(title),
                Confidence = 0f
            };
        }

        private static void ApplyDate(ParsedMagazineIssueInfo parsed, Match match, bool explicitSeparator)
        {
            parsed.IssueYear = int.Parse(match.Groups[1].Value);
            parsed.IssueMonth = int.Parse(match.Groups[2].Value);
            parsed.IssueDay = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : null;
            parsed.Confidence = parsed.IssueDay.HasValue ? 1.0f : (explicitSeparator ? 0.5f : 0.3f);
        }
    }
}
