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
        private static readonly Regex IssueNumberYearRegex = new Regex(@"\b(?:no|nr|issue)\.?\s*(\d{1,2})[\W_]*(\d{4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MonthYearRegex = new Regex(@"\b(january|february|march|april|may|june|july|august|september|october|november|december)[\W_]+(\d{4})\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex VideoReleaseRegex = new Regex(@"\bS\d{1,2}E\d{1,2}\b|\b(?:2160p|1080p|720p|WEB[-_. ]?DL|WEBRip|BluRay|x26[45]|H[ .]?26[45]|AMZN|NF|DDP?\d(?:[. ]\d)?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex LeadingNoiseRegex = new Regex(@"^(?:\[[^\]]+\][\W_]*)*(?:magazine[\W_]+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex NonAlphaNumericRegex = new Regex(@"[^a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public ParsedMagazineIssueInfo ParseFilename(string filename, string magazineFolderName)
        {
            var baseName = System.IO.Path.GetFileNameWithoutExtension(filename) ?? string.Empty;
            var parsed = ParseFolderName(magazineFolderName);
            parsed.ReleaseTitle = baseName;
            parsed.Quality = MagazineFormatDetector.DetectQuality(filename);

            if (VideoReleaseRegex.IsMatch(baseName) || !LooksLikeMagazineRelease(baseName, magazineFolderName))
            {
                parsed.Confidence = 0f;
                return parsed;
            }

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

            var issueNumberYearMatch = IssueNumberYearRegex.Match(baseName);
            if (issueNumberYearMatch.Success)
            {
                ApplyIssueNumberYear(parsed, issueNumberYearMatch);
                return parsed;
            }

            var monthYearMatch = MonthYearRegex.Match(baseName);
            if (monthYearMatch.Success)
            {
                ApplyMonthYear(parsed, monthYearMatch);
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

        private static void ApplyIssueNumberYear(ParsedMagazineIssueInfo parsed, Match match)
        {
            var issueNumber = int.Parse(match.Groups[1].Value);
            if (issueNumber is < 1 or > 12)
            {
                parsed.Confidence = 0f;
                return;
            }

            parsed.IssueYear = int.Parse(match.Groups[2].Value);
            parsed.IssueMonth = issueNumber;
            parsed.IssueDay = null;
            parsed.IssueNumber = issueNumber.ToString("D2");
            parsed.Confidence = 0.7f;
        }

        private static void ApplyMonthYear(ParsedMagazineIssueInfo parsed, Match match)
        {
            parsed.IssueYear = int.Parse(match.Groups[2].Value);
            parsed.IssueMonth = match.Groups[1].Value.ToLowerInvariant() switch
            {
                "january" => 1,
                "february" => 2,
                "march" => 3,
                "april" => 4,
                "may" => 5,
                "june" => 6,
                "july" => 7,
                "august" => 8,
                "september" => 9,
                "october" => 10,
                "november" => 11,
                "december" => 12,
                _ => 0
            };
            parsed.IssueDay = null;
            parsed.Confidence = parsed.IssueMonth > 0 ? 0.6f : 0f;
        }

        private static bool LooksLikeMagazineRelease(string baseName, string magazineFolderName)
        {
            if (string.IsNullOrWhiteSpace(baseName) || string.IsNullOrWhiteSpace(magazineFolderName))
            {
                return false;
            }

            var normalizedPrefix = LeadingNoiseRegex.Replace(baseName, string.Empty)
                .Replace('.', ' ')
                .Replace('_', ' ')
                .TrimStart();

            var normalizedReleaseTitle = MagazineTitleNormalizer.Normalize(normalizedPrefix);
            var normalizedMagazineTitle = MagazineTitleNormalizer.Normalize(magazineFolderName);

            var comparableReleaseTitle = NonAlphaNumericRegex.Replace(normalizedReleaseTitle, string.Empty);
            var comparableMagazineTitle = NonAlphaNumericRegex.Replace(normalizedMagazineTitle, string.Empty);

            return comparableReleaseTitle.StartsWith(comparableMagazineTitle, System.StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
