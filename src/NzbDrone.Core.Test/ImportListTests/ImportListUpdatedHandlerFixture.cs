using Moq;
using NUnit.Framework;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]
    public class ImportListUpdatedHandlerFixture : CoreTest<ImportListUpdatedHandler>
    {
        [Test]
        public void should_submit_durable_sync_when_import_list_is_updated()
        {
            var definition = new ImportListDefinition { Id = 23 };

            Subject.Handle(new ProviderUpdatedEvent<IImportList>(definition));

            Mocker.GetMock<IImportListSyncCommandSubmitter>()
                  .Verify(v => v.Submit(
                      It.Is<ImportListSyncCommand>(c => c.DefinitionId == 23),
                      It.IsAny<CommandPriority>(),
                      It.IsAny<CommandTrigger>()),
                      Times.Once());
        }

        [Test]
        public void should_submit_durable_sync_when_import_list_is_added()
        {
            var definition = new ImportListDefinition { Id = 41 };

            Subject.Handle(new ProviderAddedEvent<IImportList>(definition));

            Mocker.GetMock<IImportListSyncCommandSubmitter>()
                  .Verify(v => v.Submit(
                      It.Is<ImportListSyncCommand>(c => c.DefinitionId == 41),
                      It.IsAny<CommandPriority>(),
                      It.IsAny<CommandTrigger>()),
                      Times.Once());
        }
    }
}
