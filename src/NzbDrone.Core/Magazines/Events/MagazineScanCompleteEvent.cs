using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Magazines.Events
{
    public class MagazineScanCompleteEvent : IEvent
    {
        public MagazineScanCompleteEvent(List<int> magazineIds, bool force)
        {
            MagazineIds = magazineIds ?? new List<int>();
            Force = force;
        }

        public List<int> MagazineIds { get; }
        public bool Force { get; }
    }
}
