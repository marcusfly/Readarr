using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Test.Magazines.Parser
{
    [TestFixture]
    public class MagazineFormatDetectorFixture
    {
        public static object[] ComicArchiveCases =
        {
            new object[] { "issue.cbr", Quality.CBR },
            new object[] { "issue.CBZ", Quality.CBZ },
            new object[] { "issue.cBt", Quality.CBT }
        };

        [TestCaseSource(nameof(ComicArchiveCases))]
        public void should_map_comic_archive_extensions_to_specific_qualities(string filename, Quality expected)
        {
            var result = MagazineFormatDetector.DetectQuality(filename);

            result.Quality.Should().Be(expected);
        }
    }
}
