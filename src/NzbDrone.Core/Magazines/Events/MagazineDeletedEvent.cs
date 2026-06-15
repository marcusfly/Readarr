using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Magazines.Events
{
    public class MagazineDeletedEvent : IEvent
    {
        public MagazineDeletedEvent(Magazine magazine, bool deleteFiles)
        {
            Magazine = magazine;
            DeleteFiles = deleteFiles;
        }

        public Magazine Magazine { get; private set; }
        public bool DeleteFiles { get; private set; }
    }
}
