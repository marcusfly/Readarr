using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class MissingMagazineIssueSearchCommand : Command
    {
        public int? MagazineId { get; set; }

        public override bool SendUpdatesToClient => true;

        public MissingMagazineIssueSearchCommand()
        {
        }

        public MissingMagazineIssueSearchCommand(int magazineId)
        {
            MagazineId = magazineId;
        }
    }
}
