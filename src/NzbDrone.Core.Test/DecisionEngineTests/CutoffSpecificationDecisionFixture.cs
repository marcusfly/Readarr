using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class CutoffSpecificationDecisionFixture : CoreTest<CutoffSpecification>
    {
        private Author _author;

        [SetUp]
        public void SetUp()
        {
            Mocker.Resolve<UpgradableSpecification>();

            _author = Builder<Author>.CreateNew()
                                     .With(a => a.QualityProfile = new QualityProfile
                                     {
                                         UpgradeAllowed = true,
                                         Cutoff = Quality.AZW3.Id,
                                         Items = Qualities.QualityFixture.GetDefaultQualities(),
                                         FormatItems = new List<ProfileFormatItem>()
                                     })
                                     .Build();

            Mocker.GetMock<ICustomFormatCalculationService>()
                  .Setup(s => s.ParseCustomFormat(It.IsAny<BookFile>()))
                  .Returns(new List<CustomFormat>());
        }

        private RemoteBook GivenRemoteBook(Quality existingQuality, string existingPath, Quality releaseQuality)
        {
            return new RemoteBook
            {
                Author = _author,
                ParsedBookInfo = new ParsedBookInfo
                {
                    Quality = new QualityModel(releaseQuality)
                },
                Books = new List<Book>
                {
                    Builder<Book>.CreateNew()
                                 .With(b => b.BookFiles = new List<BookFile>
                                 {
                                     new BookFile
                                     {
                                         Path = existingPath,
                                         Quality = new QualityModel(existingQuality)
                                     }
                                 })
                                 .Build()
                },
                CustomFormats = new List<CustomFormat>()
            };
        }

        [Test]
        public void should_ignore_existing_ebook_when_evaluating_audiobook_cutoff()
        {
            var remoteBook = GivenRemoteBook(Quality.AZW3, "/books/book.azw3", Quality.MP3);

            Subject.IsSatisfiedBy(remoteBook, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_still_reject_same_media_type_when_cutoff_is_met()
        {
            var remoteBook = GivenRemoteBook(Quality.AZW3, "/books/book.azw3", Quality.EPUB);

            Subject.IsSatisfiedBy(remoteBook, null).Accepted.Should().BeFalse();
        }
    }
}
