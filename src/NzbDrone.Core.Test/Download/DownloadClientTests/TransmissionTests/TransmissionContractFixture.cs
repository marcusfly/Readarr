using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.Transmission;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.TransmissionTests
{
    [TestFixture]
    public class TransmissionContractFixture : DownloadClientContractTests
    {
        private Mock<ITransmissionProxy> _proxy;
        private Mock<IRemotePathMappingService> _remotePathMappingService;

        [SetUp]
        public void Setup()
        {
            _proxy = new Mock<ITransmissionProxy>(MockBehavior.Strict);
            _remotePathMappingService = new Mock<IRemotePathMappingService>(MockBehavior.Loose);
            _remotePathMappingService
                .Setup(s => s.RemapRemoteToLocal(It.IsAny<string>(), It.IsAny<OsPath>()))
                .Returns((string _, OsPath path) => path);
        }

        protected override IDownloadClientV2 CreateSubject()
        {
            var legacy = CreateLegacyClient();
            return new TransmissionV2(legacy, _proxy.Object);
        }

        protected override void GivenEmptyQueue()
        {
            _proxy.Setup(p => p.GetTorrents(It.IsAny<TransmissionSettings>()))
                 .Returns(new List<TransmissionTorrent>());
        }

        protected override void GivenAuthenticationFailure()
        {
            _proxy.Setup(p => p.GetClientVersion(It.IsAny<TransmissionSettings>()))
                 .Throws(new DownloadClientAuthenticationException("Unauthorized"));
            _proxy.Setup(p => p.GetTorrents(It.IsAny<TransmissionSettings>()))
                 .Returns(new List<TransmissionTorrent>());
        }

        [Test]
        public void GetQueue_should_map_finished_checking_torrent_to_postprocessing()
        {
            _proxy.Setup(p => p.GetTorrents(It.IsAny<TransmissionSettings>()))
                 .Returns(new List<TransmissionTorrent>
                 {
                     new TransmissionTorrent
                     {
                         HashString = "abc123",
                         Name = "Test.Book",
                         DownloadDir = "/downloads/Readarr",
                         TotalSize = 100,
                         LeftUntilDone = 0,
                         IsFinished = true,
                         Status = TransmissionTorrentStatus.CheckWait
                     }
                 });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.PostProcessing);
        }

        private Transmission CreateLegacyClient()
        {
            var legacy = new Transmission(
                _proxy.Object,
                Mock.Of<ITorrentFileInfoReader>(),
                Mock.Of<IHttpClient>(),
                Mock.Of<IConfigService>(),
                Mock.Of<IDiskProvider>(),
                _remotePathMappingService.Object,
                Mock.Of<IBlocklistService>(),
                LogManager.GetLogger("test"));

            legacy.Definition = new DownloadClientDefinition
            {
                Id = 4,
                Name = "TestTransmission",
                Settings = new TransmissionSettings
                {
                    Host = "127.0.0.1",
                    Port = 9091,
                    MusicCategory = "Readarr"
                }
            };

            return legacy;
        }
    }
}
