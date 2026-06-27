using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Parser
{
    [TestFixture]
    public class MagazineFilenameParserFixture : CoreTest<MagazineFilenameParser>
    {
        [TestCase("Motor Trend - 2024-03-01.pdf", "Motor Trend", 2024, 3, 1)]
        [TestCase("PC World – 2023-11.cbz", "PC World", 2023, 11, null)]
        [TestCase("Car and Driver — 2022-06-01.epub", "Car and Driver", 2022, 6, 1)]
        public void should_parse_dates_with_supported_separators(string filename, string magazineTitle, int year, int month, int? day)
        {
            var result = Subject.ParseFilename(filename, magazineTitle);

            result.IssueYear.Should().Be(year);
            result.IssueMonth.Should().Be(month);
            result.IssueDay.Should().Be(day);
        }

        [Test]
        public void should_parse_fallback_compact_date()
        {
            var result = Subject.ParseFilename("motortrend_202403.pdf", "Motor Trend");

            result.IssueYear.Should().Be(2024);
            result.IssueMonth.Should().Be(3);
            result.IssueDay.Should().BeNull();
            result.Confidence.Should().Be(0.3f);
        }

        [Test]
        public void should_parse_issue_number_and_year_patterns()
        {
            var result = Subject.ParseFilename("Playboy.No.02.2026.GERMAN.HYBRID.MAGAZINE.eBook-LORENZ-xpost", "Playboy");

            result.IssueYear.Should().Be(2026);
            result.IssueMonth.Should().Be(2);
            result.IssueDay.Should().BeNull();
            result.IssueNumber.Should().Be("02");
            result.Confidence.Should().Be(0.7f);
        }

        [Test]
        public void should_parse_month_name_and_year_patterns()
        {
            var result = Subject.ParseFilename("Playboy New Zealand July 2022", "Playboy");

            result.IssueYear.Should().Be(2022);
            result.IssueMonth.Should().Be(7);
            result.IssueDay.Should().BeNull();
            result.Confidence.Should().Be(0.6f);
        }

        [Test]
        public void should_reject_obvious_video_releases()
        {
            var result = Subject.ParseFilename("American.Playboy.The.Hugh.Hefner.Story.S01E05.2160p.AMZN.WEB-DL", "Playboy");

            result.Confidence.Should().Be(0f);
        }

        [Test]
        public void should_return_zero_confidence_when_no_date_is_found()
        {
            var result = Subject.ParseFilename("motortrend_special.pdf", "Motor Trend");

            result.Confidence.Should().Be(0f);
        }

        [Test]
        public void parse_folder_name_should_return_title_only()
        {
            var result = Subject.ParseFolderName("Motor Trend");

            result.MagazineTitle.Should().Be("Motor Trend");
            result.NormalizedMagazineTitle.Should().Be("motor trend");
            result.Confidence.Should().Be(0f);
        }
    }
}
