using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Magazines.Events
{
    public class MagazineAddedEvent : IEvent
    {
        public MagazineAddedEvent(Magazine magazine)
        {
            Magazine = magazine;
        }

        public Magazine Magazine { get; private set; }
    }
}
