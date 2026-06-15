using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Commands
{
    public class MagazineIssueSearchCommand : Command
    {
        public List<int> MagazineIssueIds { get; set; }

        public override bool SendUpdatesToClient => true;

        public MagazineIssueSearchCommand()
        {
        }

        public MagazineIssueSearchCommand(List<int> magazineIssueIds)
        {
            MagazineIssueIds = magazineIssueIds;
        }
    }
}
