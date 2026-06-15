using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Magazines.Events
{
    public class MagazineIssueMonitoredChangedEvent : IEvent
    {
        public MagazineIssueMonitoredChangedEvent(int magazineIssueId, bool monitored)
        {
            MagazineIssueId = magazineIssueId;
            Monitored = monitored;
        }

        public int MagazineIssueId { get; }
        public bool Monitored { get; }
    }
}
