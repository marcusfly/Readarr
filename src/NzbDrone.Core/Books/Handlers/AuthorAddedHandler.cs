using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public class AuthorAddedHandler : IHandle<AuthorAddedEvent>,
                                      IHandle<AuthorsImportedEvent>
    {
        private readonly IRefreshCommandSubmitter _refreshCommandSubmitter;

        public AuthorAddedHandler(IRefreshCommandSubmitter refreshCommandSubmitter)
        {
            _refreshCommandSubmitter = refreshCommandSubmitter;
        }

        public void Handle(AuthorAddedEvent message)
        {
            if (message.DoRefresh)
            {
                _refreshCommandSubmitter.Submit(new RefreshAuthorCommand(message.Author.Id, true));
            }
        }

        public void Handle(AuthorsImportedEvent message)
        {
            if (message.DoRefresh)
            {
                _refreshCommandSubmitter.Submit(new BulkRefreshAuthorCommand(message.AuthorIds, true));
            }
        }
    }
}
