using System;
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
        private static readonly Regex FullDateRegex = new Regex(@"(?<!\d)(\d{4})-(\d{1,2})(?:-(\d{1,2}))?(?!\d)", RegexOptions.Compiled);
        private static readonly Regex FallbackDateRegex = new Regex(@"(?<!\d)(\d{4})[\W_]?(\d{2})(?!\d)", RegexOptions.Compiled);
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
                    if (TryApplyDate(parsed, dateMatch, true))
                    {
                        return parsed;
                    }

                    parsed.Confidence = 0f;
                    return parsed;
                }
            }

            var fullDateMatch = FullDateRegex.Match(baseName);
            if (fullDateMatch.Success)
            {
                if (TryApplyDate(parsed, fullDateMatch, true))
                {
                    return parsed;
                }

                parsed.Confidence = 0f;
                return parsed;
            }

            var fallbackMatch = FallbackDateRegex.Match(baseName);
            if (fallbackMatch.Success)
            {
                if (TryApplyDate(parsed, fallbackMatch, false))
                {
                    return parsed;
                }

                parsed.Confidence = 0f;
                return parsed;
            }

            var issueNumberYearMatch = IssueNumberYearRegex.Match(baseName);
            if (issueNumberYearMatch.Success)
            {
                var explicitMonthMatch = MonthYearRegex.Match(baseName);
                ApplyIssueNumberYear(parsed, issueNumberYearMatch, explicitMonthMatch);
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

        private static bool TryApplyDate(ParsedMagazineIssueInfo parsed, Match match, bool explicitSeparator)
        {
            var year = int.Parse(match.Groups[1].Value);
            var month = int.Parse(match.Groups[2].Value);
            var day = match.Groups[3].Success ? (int?)int.Parse(match.Groups[3].Value) : null;

            if (!IsSupportedIssueYear(year))
            {
                return false;
            }

            if (month is < 1 or > 12)
            {
                return false;
            }

            if (day.HasValue && (day.Value < 1 || day.Value > DateTime.DaysInMonth(year, month)))
            {
                return false;
            }

            parsed.IssueYear = year;
            parsed.IssueMonth = month;
            parsed.IssueDay = day;
            parsed.Confidence = parsed.IssueDay.HasValue ? 1.0f : (explicitSeparator ? 0.5f : 0.3f);
            return true;
        }

        private static void ApplyIssueNumberYear(ParsedMagazineIssueInfo parsed, Match match, Match monthYearMatch)
        {
            var issueNumber = int.Parse(match.Groups[1].Value);
            if (issueNumber < 1)
            {
                parsed.Confidence = 0f;
                return;
            }

            var year = int.Parse(match.Groups[2].Value);
            if (!IsSupportedIssueYear(year))
            {
                parsed.Confidence = 0f;
                return;
            }

            parsed.IssueYear = year;
            parsed.IssueMonth = monthYearMatch.Success ? GetMonth(monthYearMatch.Groups[1].Value) : 0;
            parsed.IssueDay = null;
            parsed.IssueNumber = issueNumber.ToString("D2");
            parsed.Confidence = parsed.IssueMonth > 0 ? 0.7f : 0.4f;
        }

        private static void ApplyMonthYear(ParsedMagazineIssueInfo parsed, Match match)
        {
            var year = int.Parse(match.Groups[2].Value);
            if (!IsSupportedIssueYear(year))
            {
                parsed.Confidence = 0f;
                return;
            }

            parsed.IssueYear = year;
            parsed.IssueMonth = GetMonth(match.Groups[1].Value);
            parsed.IssueDay = null;
            parsed.Confidence = parsed.IssueMonth > 0 ? 0.6f : 0f;
        }

        private static bool IsSupportedIssueYear(int year)
        {
            var maxYear = DateTime.UtcNow.Year + 1;
            return year >= 1900 && year <= maxYear;
        }

        private static int GetMonth(string monthName)
        {
            return monthName.ToLowerInvariant() switch
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
