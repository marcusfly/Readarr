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
using NzbDrone.Core.Download.Clients.Vuze;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.VuzeTests
{
    [TestFixture]
    public class VuzeContractFixture : DownloadClientContractTests
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
            return new VuzeV2(legacy, _proxy.Object);
        }

        protected override void GivenEmptyQueue()
        {
            _proxy.Setup(p => p.GetTorrents(It.IsAny<TransmissionSettings>()))
                 .Returns(new List<TransmissionTorrent>());
        }

        protected override void GivenAuthenticationFailure()
        {
            _proxy.Setup(p => p.GetProtocolVersion(It.IsAny<TransmissionSettings>()))
                 .Throws(new DownloadClientAuthenticationException("Unauthorized"));
            _proxy.Setup(p => p.GetTorrents(It.IsAny<TransmissionSettings>()))
                 .Returns(new List<TransmissionTorrent>());
        }

        [Test]
        public void GetQueue_should_map_active_seeding_to_seeding()
        {
            _proxy.Setup(p => p.GetTorrents(It.IsAny<TransmissionSettings>()))
                 .Returns(new List<TransmissionTorrent>
                 {
                     new TransmissionTorrent
                     {
                         HashString = "vuze1",
                         Name = "Test.Book",
                         DownloadDir = "/downloads/Readarr",
                         TotalSize = 100,
                         LeftUntilDone = 0,
                         IsFinished = true,
                         Status = TransmissionTorrentStatus.Seeding
                     }
                 });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.Seeding);
        }

        private Vuze CreateLegacyClient()
        {
            var legacy = new Vuze(
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
                Id = 5,
                Name = "TestVuze",
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
