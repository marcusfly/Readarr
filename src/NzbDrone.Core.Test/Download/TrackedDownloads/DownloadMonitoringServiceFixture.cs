using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.TrackedDownloads
{
    [TestFixture]
    public class DownloadMonitoringServiceFixture : CoreTest<DownloadMonitoringService>
    {
        [Test]
        public void should_submit_process_monitored_downloads_after_refresh()
        {
            Mocker.GetMock<IDownloadClientFactory>()
                  .Setup(v => v.DownloadHandlingEnabled())
                  .Returns(new List<IDownloadClient>());

            Subject.Execute(new RefreshMonitoredDownloadsCommand());

            Mocker.GetMock<IDownloadMonitoringCommandSubmitter>()
                  .Verify(v => v.Submit(It.IsAny<ProcessMonitoredDownloadsCommand>(), CommandPriority.High, CommandTrigger.Unspecified), Times.Once());
        }
    }
}
