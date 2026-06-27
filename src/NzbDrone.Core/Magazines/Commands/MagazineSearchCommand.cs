using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class MagazineSearchCommand : Command
    {
        public int? MagazineId { get; set; }

        public override bool SendUpdatesToClient => true;

        public MagazineSearchCommand()
        {
        }

        public MagazineSearchCommand(int magazineId)
        {
            MagazineId = magazineId;
        }
    }
}
