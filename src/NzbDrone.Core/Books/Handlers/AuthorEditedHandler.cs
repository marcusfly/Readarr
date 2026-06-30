using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Books
{
    public class AuthorEditedService : IHandle<AuthorEditedEvent>
    {
        private readonly IRefreshCommandSubmitter _refreshCommandSubmitter;

        public AuthorEditedService(IRefreshCommandSubmitter refreshCommandSubmitter)
        {
            _refreshCommandSubmitter = refreshCommandSubmitter;
        }

        public void Handle(AuthorEditedEvent message)
        {
            // Refresh Author is we change BookType Preferences
            if (message.Author.MetadataProfileId != message.OldAuthor.MetadataProfileId)
            {
                _refreshCommandSubmitter.Submit(new RefreshAuthorCommand(message.Author.Id, false));
            }
        }
    }
}
