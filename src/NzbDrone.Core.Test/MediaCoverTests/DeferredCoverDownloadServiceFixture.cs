using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaCover.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaCoverTests
{
    [TestFixture]
    public class DeferredCoverDownloadServiceFixture : CoreTest<DeferredCoverDownloadService>
    {
        private Author _author;
        private Book _book;
        private MediaCover.MediaCover _authorCover;
        private MediaCover.MediaCover _bookCover;
        private DateTime _utcNow;

        [SetUp]
        public void SetUp()
        {
            _utcNow = new DateTime(2026, 6, 16, 15, 0, 0, DateTimeKind.Utc);
            _authorCover = new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://covers.example/author.jpg");
            _bookCover = new MediaCover.MediaCover(MediaCoverTypes.Cover, "https://covers.example/book.jpg");

            _author = new Author
            {
                Id = 7,
                Metadata = new AuthorMetadata
                {
                    Images = new List<MediaCover.MediaCover> { _authorCover }
                }
            };

            _book = new Book
            {
                Id = 17,
                Editions = new List<Edition>
                {
                    new Edition
                    {
                        Monitored = true,
                        Images = new List<MediaCover.MediaCover> { _bookCover }
                    }
                }
            };

            Mocker.GetMock<IAuthorService>()
                  .Setup(x => x.GetAuthor(_author.Id))
                  .Returns(_author);

            Mocker.GetMock<IBookService>()
                  .Setup(x => x.GetBooksByAuthor(_author.Id))
                  .Returns(new List<Book> { _book });

            Mocker.GetMock<IMediaCoverDownloader>()
                  .Setup(x => x.DownloadAuthorCoverIfNeeded(_author, _authorCover))
                  .Returns(true);

            Mocker.GetMock<IMediaCoverDownloader>()
                  .Setup(x => x.DownloadBookCoverIfNeeded(_book, _bookCover))
                  .Returns(true);

            Mocker.GetMock<IDeferredCoverClock>()
                  .SetupGet(x => x.UtcNow)
                  .Returns(() => _utcNow);
        }

        [Test]
        public void enqueue_should_add_to_pending_queue()
        {
            Subject.Enqueue(_author.Id, _authorCover.CoverType, _authorCover.Url);

            Subject.PendingCount.Should().Be(1);
        }

        [Test]
        public void process_should_drain_items_and_call_downloader()
        {
            Subject.Enqueue(_author.Id, _authorCover.CoverType, _authorCover.Url);
            Subject.Enqueue(_author.Id, _bookCover.CoverType, _bookCover.Url);

            Subject.Execute(new ProcessDeferredCoversCommand());

            Subject.PendingCount.Should().Be(0);
            Mocker.GetMock<IMediaCoverDownloader>().Verify(x => x.DownloadAuthorCoverIfNeeded(_author, _authorCover), Times.Once());
            Mocker.GetMock<IMediaCoverDownloader>().Verify(x => x.DownloadBookCoverIfNeeded(_book, _bookCover), Times.Once());
            Mocker.GetMock<IEventAggregator>().Verify(x => x.PublishEvent(It.Is<MediaCoversUpdatedEvent>(e => e.Author.Id == _author.Id)), Times.Once());
        }

        [Test]
        public void stale_items_should_be_skipped()
        {
            Subject.Enqueue(_author.Id, _authorCover.CoverType, _authorCover.Url);
            _utcNow = _utcNow.AddHours(25);

            Subject.Execute(new ProcessDeferredCoversCommand());

            Subject.PendingCount.Should().Be(0);
            Mocker.GetMock<IMediaCoverDownloader>().Verify(x => x.DownloadAuthorCoverIfNeeded(It.IsAny<Author>(), It.IsAny<MediaCover.MediaCover>()), Times.Never());
        }

        [Test]
        public void pending_count_should_reflect_queue_state()
        {
            Subject.Enqueue(_author.Id, _authorCover.CoverType, _authorCover.Url);
            Subject.Enqueue(_author.Id, _bookCover.CoverType, _bookCover.Url);

            Subject.PendingCount.Should().Be(2);

            Subject.Execute(new ProcessDeferredCoversCommand());

            Subject.PendingCount.Should().Be(0);
        }
    }
}
