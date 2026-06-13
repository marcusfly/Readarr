using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class RefreshBookServiceFixture : CoreTest<RefreshBookService>
    {
        private AuthorMetadata _authorMetadata;
        private Book _book;
        private Book _remoteBook;
        private Author _remoteAuthor;
        private Edition _edition;

        [SetUp]
        public void Setup()
        {
            _authorMetadata = Builder<AuthorMetadata>.CreateNew()
                .With(x => x.ForeignAuthorId = "openlibrary:OL1A")
                .Build();

            _edition = Builder<Edition>.CreateNew()
                .With(x => x.ForeignEditionId = "openlibrary:OL1M")
                .Build();

            _book = Builder<Book>.CreateNew()
                .With(x => x.Id = 10)
                .With(x => x.ForeignBookId = "openlibrary:OL1W")
                .With(x => x.AuthorMetadata = _authorMetadata)
                .With(x => x.Editions = new List<Edition> { _edition })
                .Build();

            _remoteBook = _book.JsonClone();
            _remoteBook.AuthorMetadata = _authorMetadata.JsonClone();
            _remoteBook.Editions = new List<Edition> { _edition.JsonClone() };

            _remoteAuthor = Builder<Author>.CreateNew()
                .With(x => x.Metadata = _authorMetadata.JsonClone())
                .With(x => x.Books = new List<Book> { _remoteBook })
                .With(x => x.Series = new List<Series>())
                .Build();

            Mocker.GetMock<IBookService>()
                .Setup(x => x.UpdateMany(It.IsAny<List<Book>>()));

            Mocker.GetMock<IEditionService>()
                .Setup(x => x.GetEditionsForRefresh(_book.Id, It.IsAny<List<string>>()))
                .Returns(new List<Edition> { _edition });

            Mocker.GetMock<IEditionService>()
                .Setup(x => x.InsertMany(It.IsAny<List<Edition>>()));

            Mocker.GetMock<IMediaFileService>()
                .Setup(x => x.GetFilesByBook(_book.Id))
                .Returns(new List<BookFile>());
        }

        [Test]
        public void should_preserve_book_when_remote_item_is_missing()
        {
            var lastInfoSync = DateTime.UtcNow.AddDays(-7);
            _book.LastInfoSync = lastInfoSync;

            Subject.RefreshBookInfo(_book, new List<Book>(), _remoteAuthor, false);

            Assert.That(_book.LastInfoSync, Is.EqualTo(lastInfoSync));
            Mocker.GetMock<IBookService>()
                .Verify(x => x.DeleteBook(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<IBookService>()
                .Verify(x => x.UpdateMany(It.IsAny<List<Book>>()), Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_preserve_book_when_edition_collection_is_incomplete()
        {
            var lastInfoSync = DateTime.UtcNow.AddDays(-7);
            _book.LastInfoSync = lastInfoSync;
            _remoteBook.Editions = new LazyLoaded<List<Edition>>();

            Subject.RefreshBookInfo(_book, new List<Book> { _remoteBook }, _remoteAuthor, false);

            Assert.That(_book.LastInfoSync, Is.EqualTo(lastInfoSync));
            Mocker.GetMock<IRefreshEditionService>()
                .Verify(x => x.RefreshEditionInfo(It.IsAny<List<Edition>>(),
                                                  It.IsAny<List<Edition>>(),
                                                  It.IsAny<List<Tuple<Edition, Edition>>>(),
                                                  It.IsAny<List<Edition>>(),
                                                  It.IsAny<List<Edition>>(),
                                                  It.IsAny<List<Edition>>(),
                                                  It.IsAny<bool>()),
                        Times.Never());
            Mocker.GetMock<IBookService>()
                .Verify(x => x.DeleteBook(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_preserve_missing_editions_when_remote_collection_is_smaller()
        {
            var secondEdition = Builder<Edition>.CreateNew()
                .With(x => x.ForeignEditionId = "openlibrary:OL2M")
                .Build();

            Mocker.GetMock<IEditionService>()
                .Setup(x => x.GetEditionsForRefresh(_book.Id, It.IsAny<List<string>>()))
                .Returns(new List<Edition> { _edition, secondEdition });

            List<Edition> deleted = null;
            Mocker.GetMock<IRefreshEditionService>()
                .Setup(x => x.RefreshEditionInfo(It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Tuple<Edition, Edition>>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<bool>()))
                .Callback<List<Edition>,
                          List<Edition>,
                          List<Tuple<Edition, Edition>>,
                          List<Edition>,
                          List<Edition>,
                          List<Edition>,
                          bool>((_, _, _, remove, _, _, _) => deleted = remove)
                .Returns(false);

            Subject.RefreshBookInfo(_book, new List<Book> { _remoteBook }, _remoteAuthor, false);

            Assert.That(deleted, Is.Empty);
        }

        [Test]
        public void should_not_advance_last_info_sync_when_edition_refresh_fails()
        {
            var lastInfoSync = DateTime.UtcNow.AddDays(-7);
            _book.LastInfoSync = lastInfoSync;

            Mocker.GetMock<IRefreshEditionService>()
                .Setup(x => x.RefreshEditionInfo(It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Tuple<Edition, Edition>>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<bool>()))
                .Throws(new InvalidOperationException("edition refresh failed"));

            Assert.Throws<InvalidOperationException>(() =>
                Subject.RefreshBookInfo(_book, new List<Book> { _remoteBook }, _remoteAuthor, false));

            Assert.That(_book.LastInfoSync, Is.EqualTo(lastInfoSync));
            Mocker.GetMock<IBookService>()
                .Verify(x => x.UpdateMany(It.IsAny<List<Book>>()), Times.Never());
        }

        [Test]
        public void should_advance_last_info_sync_after_complete_refresh()
        {
            var lastInfoSync = DateTime.UtcNow.AddDays(-7);
            _book.LastInfoSync = lastInfoSync;

            Mocker.GetMock<IRefreshEditionService>()
                .Setup(x => x.RefreshEditionInfo(It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Tuple<Edition, Edition>>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<List<Edition>>(),
                                                 It.IsAny<bool>()))
                .Returns(false);

            Subject.RefreshBookInfo(_book, new List<Book> { _remoteBook }, _remoteAuthor, false);

            Assert.That(_book.LastInfoSync, Is.GreaterThan(lastInfoSync));
        }
    }
}
