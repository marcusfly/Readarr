using System;
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
            result.IssueMonth.Should().Be(0);
            result.IssueDay.Should().BeNull();
            result.IssueNumber.Should().Be("02");
            result.Confidence.Should().Be(0.4f);
        }

        [Test]
        public void should_only_apply_issue_number_as_month_when_explicit_month_name_exists()
        {
            var result = Subject.ParseFilename("Playboy.No.02.2026.February.2026.pdf", "Playboy");

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

        [TestCase("Motor Trend - 2024-13-01.pdf")]
        [TestCase("Motor Trend - 2024-02-30.pdf")]
        [TestCase("motortrend_202413.pdf")]
        public void should_reject_invalid_date_matches(string filename)
        {
            var result = Subject.ParseFilename(filename, "Motor Trend");

            result.IssueYear.Should().Be(0);
            result.IssueMonth.Should().Be(0);
            result.IssueDay.Should().BeNull();
            result.Confidence.Should().Be(0f);
        }

        [TestCase("Motor Trend - 1899-12-01.pdf")]
        [TestCase("Motor Trend January 1899.pdf")]
        public void should_reject_unrealistic_historical_years(string filename)
        {
            var result = Subject.ParseFilename(filename, "Motor Trend");

            result.IssueYear.Should().Be(0);
            result.IssueMonth.Should().Be(0);
            result.IssueDay.Should().BeNull();
            result.Confidence.Should().Be(0f);
        }

        [Test]
        public void should_reject_unrealistic_future_years()
        {
            var futureYear = DateTime.UtcNow.Year + 2;
            var result = Subject.ParseFilename($"Motor Trend - {futureYear}-03-01.pdf", "Motor Trend");

            result.IssueYear.Should().Be(0);
            result.IssueMonth.Should().Be(0);
            result.IssueDay.Should().BeNull();
            result.Confidence.Should().Be(0f);
        }

        [Test]
        public void should_not_match_compact_date_embedded_in_longer_number()
        {
            var result = Subject.ParseFilename("Motor Trend 2024031.pdf", "Motor Trend");

            result.IssueYear.Should().Be(0);
            result.IssueMonth.Should().Be(0);
            result.IssueDay.Should().BeNull();
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
