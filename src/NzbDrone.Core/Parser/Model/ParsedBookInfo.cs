using System.Collections.Generic;
using System.Text.Json.Serialization;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Parser.Model
{
    public class ParsedBookInfo
    {
        public string BookTitle { get; set; }
        public string AuthorName { get; set; }
        public AuthorTitleInfo AuthorTitleInfo { get; set; }
        public QualityModel Quality { get; set; }
        public string ReleaseDate { get; set; }
        public bool Discography { get; set; }
        public int DiscographyStart { get; set; }
        public int DiscographyEnd { get; set; }
        public string ReleaseGroup { get; set; }
        public string ReleaseHash { get; set; }
        public string ReleaseVersion { get; set; }
        public string ReleaseTitle { get; set; }

        /// <summary>
        /// Confidence of the parse result, from 0 (no confidence) to 1 (certain).
        /// Set by the parser based on how many fields were successfully extracted.
        /// </summary>
        public float Confidence { get; set; } = 1.0f;

        /// <summary>
        /// When a release is rejected during matching, this contains the human-readable
        /// reason. Null when the release has not been rejected.
        /// </summary>
        public string RejectionReason { get; set; }

        [JsonIgnore]
        public Dictionary<string, object> ExtraInfo { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            var bookString = "[Unknown Book]";

            if (BookTitle != null)
            {
                bookString = string.Format("{0}", BookTitle);
            }

            return string.Format("{0} - {1} {2}", AuthorName, bookString, Quality);
        }
    }
}
