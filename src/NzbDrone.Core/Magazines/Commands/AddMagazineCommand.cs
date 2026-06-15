using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class AddMagazineCommand : Command
    {
        public Magazine Magazine { get; set; }
        public override bool SendUpdatesToClient => true;
    }
}
