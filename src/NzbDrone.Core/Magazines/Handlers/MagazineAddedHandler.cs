using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines
{
    public class MagazineAddedHandler : IHandle<Magazines.Events.MagazineAddedEvent>
    {
        private readonly IManageCommandQueue _commandQueueManager;
        private readonly IMagazineMonitoredService _magazineMonitoredService;

        public MagazineAddedHandler(IManageCommandQueue commandQueueManager, IMagazineMonitoredService magazineMonitoredService)
        {
            _commandQueueManager = commandQueueManager;
            _magazineMonitoredService = magazineMonitoredService;
        }

        public void Handle(Magazines.Events.MagazineAddedEvent message)
        {
            var magazine = message.Magazine;

            if (magazine?.AddOptions != null)
            {
                _magazineMonitoredService.SetIssueMonitoredStatus(magazine, magazine.AddOptions.Monitor);

                if (magazine.AddOptions.SearchForMissingIssues)
                {
                    _commandQueueManager.Push(new NzbDrone.Core.Magazines.Commands.MagazineSearchCommand(magazine.Id));
                }
            }
        }
    }
}
