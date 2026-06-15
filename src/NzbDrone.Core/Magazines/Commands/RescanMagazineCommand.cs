using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class RescanMagazineCommand : Command
    {
        public List<int> MagazineIds { get; set; }
        public bool AddNewIssues { get; set; }

        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
    }
}
