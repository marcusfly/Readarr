using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Parser
{
    public static class FtsNormalization
    {
        private static readonly Regex PossessiveRegex = new Regex(@"['\u2019]s\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex AndSymbolRegex = new Regex(@"\s*[&+]\s*", RegexOptions.Compiled);
        private static readonly Regex VsRegex = new Regex(@"\bvs\.\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DrRegex = new Regex(@"\bdr\.\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MrRegex = new Regex(@"\bmr\.\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex PunctuationRegex = new Regex(@"[^\p{L}\p{Nd}\s]", RegexOptions.Compiled);
        private static readonly Regex FormatSuffixRegex = new Regex(@"\b(?:unabridged|abridged|audiobook|ebook|epub|m4b|mp3)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var normalized = input.Trim().ToLowerInvariant();
            normalized = normalized.RemoveAccent();
            normalized = PossessiveRegex.Replace(normalized, string.Empty);
            normalized = AndSymbolRegex.Replace(normalized, " and ");
            normalized = VsRegex.Replace(normalized, "vs");
            normalized = DrRegex.Replace(normalized, "dr");
            normalized = MrRegex.Replace(normalized, "mr");
            normalized = PunctuationRegex.Replace(normalized, " ");
            normalized = FormatSuffixRegex.Replace(normalized, " ");

            return normalized.CleanSpaces();
        }

        public static IEnumerable<string> NormalizeValues(IEnumerable<string> inputs)
        {
            if (inputs == null)
            {
                return Enumerable.Empty<string>();
            }

            return inputs.Select(Normalize);
        }
    }
}
