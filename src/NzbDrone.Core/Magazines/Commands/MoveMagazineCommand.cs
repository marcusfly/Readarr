using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class MoveMagazineCommand : Command
    {
        public int MagazineId { get; set; }
        public string SourcePath { get; set; }
        public string DestinationPath { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
    }
}
