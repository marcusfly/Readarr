using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Magazines.Events
{
    public class MagazineUpdatedEvent : IEvent
    {
        public MagazineUpdatedEvent(Magazine magazine, Magazine previousMagazine)
        {
            Magazine = magazine;
            PreviousMagazine = previousMagazine;
        }

        public Magazine Magazine { get; private set; }
        public Magazine PreviousMagazine { get; private set; }
    }
}
