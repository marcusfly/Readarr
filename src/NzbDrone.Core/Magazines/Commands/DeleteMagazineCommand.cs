using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class DeleteMagazineCommand : Command
    {
        public int MagazineId { get; set; }
        public bool DeleteFiles { get; set; }

        public override bool SendUpdatesToClient => true;
    }
}
