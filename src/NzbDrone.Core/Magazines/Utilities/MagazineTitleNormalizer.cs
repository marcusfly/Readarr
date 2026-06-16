using System.Text.RegularExpressions;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Magazines
{
    public static class MagazineTitleNormalizer
    {
        private static readonly Regex LeadingTheRegex = new Regex(@"^the\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PunctuationRegex = new Regex(@"[\p{P}\p{S}]", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);

        public static string Normalize(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return string.Empty;
            }

            var normalized = FtsNormalization.Normalize(rawTitle);
            normalized = LeadingTheRegex.Replace(normalized, string.Empty);
            normalized = PunctuationRegex.Replace(normalized, string.Empty);
            normalized = WhitespaceRegex.Replace(normalized, " ");

            return normalized.Trim();
        }
    }

    public interface IBuildMagazinePaths
    {
        string BuildPath(Magazine magazine);
    }

    public class MagazinePathBuilder : IBuildMagazinePaths
    {
        public string BuildPath(Magazine magazine)
        {
            if (magazine == null)
            {
                throw new System.ArgumentNullException(nameof(magazine));
            }

            if (string.IsNullOrWhiteSpace(magazine.RootFolderPath))
            {
                throw new System.ArgumentException("Root folder was not provided", nameof(magazine));
            }

            var folderName = string.IsNullOrWhiteSpace(magazine.NormalizedTitle)
                ? MagazineTitleNormalizer.Normalize(magazine.Title)
                : magazine.NormalizedTitle;

            return System.IO.Path.Combine(magazine.RootFolderPath, folderName);
        }
    }
}
