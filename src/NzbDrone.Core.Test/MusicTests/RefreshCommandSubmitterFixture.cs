using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class RefreshCommandSubmitterFixture : CoreTest<RefreshCommandSubmitter>
    {
        [Test]
        public void should_submit_refresh_author_with_stable_idempotency_key()
        {
            var command = new RefreshAuthorCommand(12, true);

            Subject.Submit(command, CommandPriority.High, CommandTrigger.Manual);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "refresh-author:12:True", CommandPriority.High, CommandTrigger.Manual), Times.Once());
        }

        [Test]
        public void should_normalize_bulk_refresh_author_ids_in_idempotency_key()
        {
            var command = new BulkRefreshAuthorCommand(new List<int> { 5, 2, 5 });

            Subject.Submit(command);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "bulk-refresh-author:2,5:False", CommandPriority.Normal, CommandTrigger.Unspecified), Times.Once());
        }

        [Test]
        public void should_submit_refresh_book_all_with_all_key()
        {
            var command = new RefreshBookCommand(null);

            Subject.Submit(command);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "refresh-book:all", CommandPriority.Normal, CommandTrigger.Unspecified), Times.Once());
        }

        [Test]
        public void should_hash_long_bulk_refresh_book_id_lists()
        {
            var ids = new List<int>();

            for (var i = 0; i < 80; i++)
            {
                ids.Add(i);
            }

            var command = new BulkRefreshBookCommand(ids);

            Subject.Submit(command);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command,
                      It.Is<string>(key => key.StartsWith("bulk-refresh-book:80:") && key.Length > "bulk-refresh-book:80:".Length),
                      CommandPriority.Normal,
                      CommandTrigger.Unspecified),
                      Times.Once());
        }
    }
}
