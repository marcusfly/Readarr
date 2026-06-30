using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AuthorAddedHandlerFixture : CoreTest<AuthorAddedHandler>
    {
        [Test]
        public void should_submit_refresh_for_new_author_when_requested()
        {
            Subject.Handle(new AuthorAddedEvent(new Author { Id = 42 }, true));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.Is<RefreshAuthorCommand>(c => c.AuthorId == 42 && c.IsNewAuthor)), Times.Once());
        }

        [Test]
        public void should_not_submit_refresh_for_new_author_when_disabled()
        {
            Subject.Handle(new AuthorAddedEvent(new Author { Id = 42 }, false));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.IsAny<RefreshAuthorCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }

        [Test]
        public void should_submit_bulk_refresh_for_imported_authors_when_requested()
        {
            Subject.Handle(new AuthorsImportedEvent(new List<int> { 4, 8 }, true));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.Is<BulkRefreshAuthorCommand>(c => c.AreNewAuthors && c.AuthorIds.Count == 2), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once());
        }
    }

    [TestFixture]
    public class AuthorEditedServiceFixture : CoreTest<AuthorEditedService>
    {
        [Test]
        public void should_submit_refresh_when_metadata_profile_changes()
        {
            Subject.Handle(new AuthorEditedEvent(
                new Author { Id = 7, MetadataProfileId = 2 },
                new Author { Id = 7, MetadataProfileId = 1 }));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.Is<RefreshAuthorCommand>(c => c.AuthorId == 7 && !c.IsNewAuthor), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once());
        }

        [Test]
        public void should_not_submit_refresh_when_metadata_profile_is_unchanged()
        {
            Subject.Handle(new AuthorEditedEvent(
                new Author { Id = 7, MetadataProfileId = 2 },
                new Author { Id = 7, MetadataProfileId = 2 }));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.IsAny<RefreshAuthorCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }
    }

    [TestFixture]
    public class BookAddedHandlerFixture : CoreTest<BookAddedHandler>
    {
        [Test]
        public void should_submit_refresh_for_parent_author_when_requested()
        {
            Subject.Handle(new BookAddedEvent(new Book
            {
                Author = new Author { Id = 13 }
            }, true));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.Is<RefreshAuthorCommand>(c => c.AuthorId == 13 && !c.IsNewAuthor), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once());
        }

        [Test]
        public void should_not_submit_refresh_for_parent_author_when_disabled()
        {
            Subject.Handle(new BookAddedEvent(new Book
            {
                Author = new Author { Id = 13 }
            }, false));

            Mocker.GetMock<IRefreshCommandSubmitter>()
                  .Verify(v => v.Submit(It.IsAny<RefreshAuthorCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Never());
        }
    }
}
