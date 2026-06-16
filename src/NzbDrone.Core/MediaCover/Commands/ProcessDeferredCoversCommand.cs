using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.MediaCover.Commands
{
    public class ProcessDeferredCoversCommand : Command
    {
        public override bool SendUpdatesToClient => false;
        public override string CompletionMessage => "Processed deferred covers";
    }
}
