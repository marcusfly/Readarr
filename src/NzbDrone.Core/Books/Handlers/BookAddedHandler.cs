using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public class BookAddedHandler : IHandle<BookAddedEvent>
    {
        private readonly IRefreshCommandSubmitter _refreshCommandSubmitter;

        public BookAddedHandler(IRefreshCommandSubmitter refreshCommandSubmitter)
        {
            _refreshCommandSubmitter = refreshCommandSubmitter;
        }

        public void Handle(BookAddedEvent message)
        {
            if (message.DoRefresh)
            {
                _refreshCommandSubmitter.Submit(new RefreshAuthorCommand(message.Book.Author.Value.Id));
            }
        }
    }
}
