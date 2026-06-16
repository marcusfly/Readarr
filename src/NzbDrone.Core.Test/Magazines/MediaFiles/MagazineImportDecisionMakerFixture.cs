using System.Collections.Generic;
using System.IO.Abstractions;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.MediaFiles;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.MediaFiles
{
    [TestFixture]
    public class MagazineImportDecisionMakerFixture : CoreTest<MagazineImportDecisionMaker>
    {
        private Magazine _magazine;
        private IFileInfo _file;
        private Mock<IFileInfo> _fileMock;

        [SetUp]
        public void SetUp()
        {
            _magazine = new Magazine { Id = 5, Title = "Motor Trend" };
            _fileMock = new Mock<IFileInfo>();
            _fileMock.SetupGet(x => x.FullName).Returns(@"C:\magazines\motor-trend\Motor Trend - 2024-03-01.pdf");
            _fileMock.SetupGet(x => x.Name).Returns("Motor Trend - 2024-03-01.pdf");
            _fileMock.SetupGet(x => x.Length).Returns(100);
            _file = _fileMock.Object;

            Mocker.GetMock<IMagazineFilenameParser>()
                .Setup(x => x.ParseFilename(_file.Name, _magazine.Title))
                .Returns(new ParsedMagazineIssueInfo
                {
                    MagazineTitle = _magazine.Title,
                    NormalizedMagazineTitle = "motor trend",
                    IssueYear = 2024,
                    IssueMonth = 3,
                    IssueDay = 1,
                    Confidence = 1.0f,
                    Quality = new QualityModel(Quality.PDF)
                });

            Mocker.GetMock<IMagazineIssueService>()
                .Setup(x => x.GetIssuesByMagazine(_magazine.Id))
                .Returns(new List<MagazineIssue>());

            Mocker.GetMock<IMagazineIssueFileService>()
                .Setup(x => x.GetFilesByMagazine(_magazine.Id))
                .Returns(new List<MagazineIssueFile>());
        }

        [Test]
        public void should_reject_already_imported_files()
        {
            Mocker.GetMock<IMagazineIssueFileService>()
                .Setup(x => x.GetFilesByMagazine(_magazine.Id))
                .Returns(new List<MagazineIssueFile>
                {
                    new MagazineIssueFile { Path = _file.FullName }
                });

            var result = Subject.GetImportDecisions(new List<IFileInfo> { _file }, _magazine, new ParsedMagazineIssueInfo { MagazineTitle = _magazine.Title });

            result.Should().ContainSingle();
            result[0].Approved.Should().BeFalse();
            result[0].Rejections[0].Reason.Should().Be("AlreadyImported");
        }

        [Test]
        public void should_reject_confidence_zero_parses()
        {
            Mocker.GetMock<IMagazineFilenameParser>()
                .Setup(x => x.ParseFilename(_file.Name, _magazine.Title))
                .Returns(new ParsedMagazineIssueInfo
                {
                    MagazineTitle = _magazine.Title,
                    Confidence = 0f,
                    Quality = new QualityModel(Quality.Unknown)
                });

            var result = Subject.GetImportDecisions(new List<IFileInfo> { _file }, _magazine, new ParsedMagazineIssueInfo { MagazineTitle = _magazine.Title });

            result[0].Approved.Should().BeFalse();
            result[0].Rejections[0].Reason.Should().Be("ParseFailed");
        }

        [Test]
        public void should_approve_new_issue_files()
        {
            var result = Subject.GetImportDecisions(new List<IFileInfo> { _file }, _magazine, new ParsedMagazineIssueInfo { MagazineTitle = _magazine.Title });

            result[0].Approved.Should().BeTrue();
            result[0].LocalIssue.Magazine.Should().Be(_magazine);
            result[0].LocalIssue.ParsedInfo.IssueYear.Should().Be(2024);
        }
    }
}
