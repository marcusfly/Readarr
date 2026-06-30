using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.ImportLists
{
    public class ImportListUpdatedHandler : IHandle<ProviderUpdatedEvent<IImportList>>, IHandle<ProviderAddedEvent<IImportList>>
    {
        private readonly IImportListSyncCommandSubmitter _importListSyncCommandSubmitter;

        public ImportListUpdatedHandler(IImportListSyncCommandSubmitter importListSyncCommandSubmitter)
        {
            _importListSyncCommandSubmitter = importListSyncCommandSubmitter;
        }

        public void Handle(ProviderUpdatedEvent<IImportList> message)
        {
            _importListSyncCommandSubmitter.Submit(new ImportListSyncCommand(message.Definition.Id));
        }

        public void Handle(ProviderAddedEvent<IImportList> message)
        {
            _importListSyncCommandSubmitter.Submit(new ImportListSyncCommand(message.Definition.Id));
        }
    }
}
