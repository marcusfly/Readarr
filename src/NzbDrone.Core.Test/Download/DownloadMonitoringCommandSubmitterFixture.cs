using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Jobs.Durable;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class DownloadMonitoringCommandSubmitterFixture : CoreTest<DownloadMonitoringCommandSubmitter>
    {
        [Test]
        public void should_submit_refresh_monitored_downloads_with_stable_idempotency_key()
        {
            var command = new RefreshMonitoredDownloadsCommand();

            Subject.Submit(command, CommandPriority.High, CommandTrigger.Scheduled);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "refresh-monitored-downloads", CommandPriority.High, CommandTrigger.Scheduled), Times.Once());
        }

        [Test]
        public void should_submit_process_monitored_downloads_with_stable_idempotency_key()
        {
            var command = new ProcessMonitoredDownloadsCommand();

            Subject.Submit(command, CommandPriority.High, CommandTrigger.Manual);

            Mocker.GetMock<IDurableJobScheduler>()
                  .Verify(v => v.Submit(command, "process-monitored-downloads", CommandPriority.High, CommandTrigger.Manual), Times.Once());
        }
    }
}
