using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Magazines.Events
{
    public class MagazineIssueFileImportedEvent : IEvent
    {
        public MagazineIssueFileImportedEvent(MagazineIssue magazineIssue, MagazineIssueFile magazineIssueFile)
        {
            MagazineIssue = magazineIssue;
            MagazineIssueFile = magazineIssueFile;
        }

        public MagazineIssue MagazineIssue { get; }
        public MagazineIssueFile MagazineIssueFile { get; }
    }
}
