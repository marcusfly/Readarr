using System.Collections.Generic;

namespace NzbDrone.Core.MediaCover
{
    public interface IDeferredCoverService
    {
        void Enqueue(int authorId, MediaCoverTypes coverType, string url);
        void EnqueueAll(int authorId, IEnumerable<(MediaCoverTypes type, string url)> covers);
        int PendingCount { get; }
    }
}
