using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class QueueServiceFixture : CoreTest<QueueService>
    {
        private List<TrackedDownload> _trackedDownloads;

        [SetUp]
        public void SetUp()
        {
            var downloadClientInfo = Builder<DownloadClientItemClientInfo>.CreateNew().Build();

            var downloadItem = Builder<NzbDrone.Core.Download.DownloadClientItem>.CreateNew()
                .With(v => v.RemainingTime = TimeSpan.FromSeconds(10))
                .With(v => v.DownloadClientInfo = downloadClientInfo)
                .Build();

            var author = Builder<Author>.CreateNew()
                .Build();

            var books = Builder<Book>.CreateListOfSize(3)
                .All()
                .With(e => e.AuthorId = author.Id)
                .Build();

            var remoteBook = Builder<RemoteBook>.CreateNew()
                .With(r => r.Author = author)
                .With(r => r.Books = new List<Book>(books))
                .With(r => r.ParsedBookInfo = new ParsedBookInfo())
                .Build();

            _trackedDownloads = Builder<TrackedDownload>.CreateListOfSize(1)
                .All()
                .With(v => v.IsTrackable = true)
                .With(v => v.DownloadItem = downloadItem)
                .With(v => v.RemoteBook = remoteBook)
                .Build()
                .ToList();

            var historyItem = Builder<EntityHistory>.CreateNew()
                .Build();

            Mocker.GetMock<IHistoryService>()
                .Setup(c => c.Find(It.IsAny<string>(), EntityHistoryEventType.Grabbed)).Returns(
                    new List<EntityHistory> { historyItem });
        }

        [Test]
        public void queue_items_should_have_id()
        {
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();

            queue.Should().HaveCount(3);

            queue.All(v => v.Id > 0).Should().BeTrue();

            var distinct = queue.Select(v => v.Id).Distinct().ToArray();

            distinct.Should().HaveCount(3);
        }

        [Test]
        public void should_create_single_queue_item_for_magazine_downloads()
        {
            var magazine = new Magazine
            {
                Id = 7,
                Title = "Playboy"
            };

            var issue = new MagazineIssue
            {
                Id = 42,
                MagazineId = 7,
                IssueYear = 2025,
                IssueMonth = 2,
                ReleaseTitle = "Playboy 2025-02"
            };

            var trackedDownload = _trackedDownloads.Single();
            trackedDownload.RemoteBook = new RemoteMagazineIssue
            {
                Magazine = magazine,
                Issue = issue,
                ParsedMagazineIssueInfo = new ParsedMagazineIssueInfo
                {
                    MagazineTitle = magazine.Title,
                    IssueYear = 2025,
                    IssueMonth = 2,
                    Confidence = 0.6f
                },
                ParsedBookInfo = new ParsedBookInfo()
            };

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();

            queue.Should().HaveCount(1);
            queue.Single().Magazine.Should().NotBeNull();
            queue.Single().Magazine.Title.Should().Be("Playboy");
            queue.Single().MagazineIssue.ReleaseTitle.Should().Be("Playboy 2025-02");
        }
    }
}
