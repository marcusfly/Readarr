using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.TagExtraction;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles.TagExtraction
{
    [TestFixture]
    public class TagExtractionServiceFixture : CoreTest<TagExtractionService>
    {
        private Dictionary<string, DateTime> _lastWriteTimes;
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = "/tmp/test-book.mp3";
            _lastWriteTimes = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase)
            {
                [_path] = new DateTime(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc)
            };

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.GetFileInfo(It.IsAny<string>()))
                .Returns((string path) => BuildFileInfo(path).Object);

            Mocker.GetMock<IAudioTagService>()
                .Setup(x => x.ReadTags(It.IsAny<string>()))
                .Returns(new ParsedTrackInfo
                {
                    BookTitle = "Mistborn",
                    Authors = { "Brandon Sanderson" },
                    Quality = new QualityModel(Quality.MP3)
                });
        }

        [Test]
        public void cache_hit_should_not_reread_file()
        {
            Subject.GetTags(_path);
            Subject.GetTags(_path);

            Mocker.GetMock<IAudioTagService>().Verify(x => x.ReadTags(_path), Times.Once());
        }

        [Test]
        public void cache_miss_should_reread_when_mtime_changes()
        {
            Subject.GetTags(_path);
            _lastWriteTimes[_path] = _lastWriteTimes[_path].AddMinutes(1);

            Subject.GetTags(_path);

            Mocker.GetMock<IAudioTagService>().Verify(x => x.ReadTags(_path), Times.Exactly(2));
        }

        [Test]
        public void evict_should_remove_cached_entry()
        {
            Subject.GetTags(_path);
            Subject.Evict(_path);
            Subject.GetTags(_path);

            Mocker.GetMock<IAudioTagService>().Verify(x => x.ReadTags(_path), Times.Exactly(2));
        }

        [Test]
        public void should_evict_least_recently_used_entry_when_capacity_exceeded()
        {
            for (var i = 0; i < 500; i++)
            {
                Subject.GetTags($"/tmp/book-{i}.mp3");
            }

            Subject.GetTags("/tmp/book-0.mp3");
            Subject.GetTags("/tmp/book-500.mp3");
            Subject.GetTags("/tmp/book-1.mp3");

            Mocker.GetMock<IAudioTagService>().Verify(x => x.ReadTags("/tmp/book-1.mp3"), Times.Exactly(2));
        }

        [Test]
        public void should_map_parsed_track_info_into_file_tag_result()
        {
            var result = Subject.GetTags(_path);

            result.Title.Should().Be("Mistborn");
            result.Author.Should().Be("Brandon Sanderson");
            result.Quality.Should().BeEquivalentTo(new QualityModel(Quality.MP3));
            result.ParsedTrackInfo.Should().NotBeNull();
        }

        private Mock<IFileInfo> BuildFileInfo(string path)
        {
            if (!_lastWriteTimes.ContainsKey(path))
            {
                _lastWriteTimes[path] = new DateTime(2026, 6, 16, 10, 0, 0, DateTimeKind.Utc).AddSeconds(_lastWriteTimes.Count);
            }

            var fileInfo = new Mock<IFileInfo>();
            fileInfo.SetupGet(x => x.FullName).Returns(path);
            fileInfo.SetupGet(x => x.LastWriteTimeUtc).Returns(() => _lastWriteTimes[path]);

            return fileInfo;
        }
    }
}
