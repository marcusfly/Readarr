using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.Sabnzbd;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.SabnzbdTests
{
    [TestFixture]
    public class SabnzbdContractFixture : DownloadClientContractTests
    {
        private Mock<ISabnzbdProxy> _proxy;
        private Mock<IRemotePathMappingService> _remotePathMappingService;
        private Mock<IConfigService> _configService;

        [SetUp]
        public void Setup()
        {
            _proxy = new Mock<ISabnzbdProxy>(MockBehavior.Strict);
            _remotePathMappingService = new Mock<IRemotePathMappingService>(MockBehavior.Loose);
            _remotePathMappingService
                .Setup(s => s.RemapRemoteToLocal(It.IsAny<string>(), It.IsAny<OsPath>()))
                .Returns((string _, OsPath path) => path);

            _configService = new Mock<IConfigService>(MockBehavior.Loose);
            _configService.SetupGet(s => s.DownloadClientHistoryLimit).Returns(30);
        }

        protected override IDownloadClientV2 CreateSubject()
        {
            return new SabnzbdV2(CreateLegacyClient());
        }

        protected override void GivenEmptyQueue()
        {
            _proxy.Setup(p => p.GetQueue(0, 0, It.IsAny<SabnzbdSettings>()))
                 .Returns(new SabnzbdQueue { Paused = false, Items = new List<SabnzbdQueueItem>() });
            _proxy.Setup(p => p.GetHistory(0, 30, It.IsAny<SabnzbdSettings>()))
                 .Returns(new SabnzbdHistory { Items = new List<SabnzbdHistoryItem>() });
        }

        protected override void GivenAuthenticationFailure()
        {
            _proxy.Setup(p => p.GetVersion(It.IsAny<SabnzbdSettings>()))
                 .Throws(new Exception("Unable to connect to SABnzbd"));
            _proxy.Setup(p => p.GetConfig(It.IsAny<SabnzbdSettings>()))
                 .Returns(new SabnzbdConfig
                 {
                     Categories = new List<SabnzbdCategory>
                     {
                         new SabnzbdCategory { Name = "Readarr", Dir = "Readarr", FullPath = new OsPath("/downloads/Readarr") },
                         new SabnzbdCategory { Name = "*", Dir = string.Empty, FullPath = new OsPath("/downloads") }
                     },
                     Misc = new SabnzbdConfigMisc { complete_dir = "/downloads" }
                 });
        }

        [Test]
        public void GetQueue_should_map_failed_history_disk_space_to_warning()
        {
            _proxy.Setup(p => p.GetQueue(0, 0, It.IsAny<SabnzbdSettings>()))
                 .Returns(new SabnzbdQueue { Paused = false, Items = new List<SabnzbdQueueItem>() });
            _proxy.Setup(p => p.GetHistory(0, 30, It.IsAny<SabnzbdSettings>()))
                 .Returns(new SabnzbdHistory
                 {
                     Items = new List<SabnzbdHistoryItem>
                     {
                         new SabnzbdHistoryItem
                         {
                             Id = "sab-1",
                             Category = "Readarr",
                             Title = "Test.Book",
                             Size = 100,
                             Status = SabnzbdDownloadStatus.Failed,
                             FailMessage = "Unpacking failed, write error or disk is full?",
                             Storage = "/downloads/Readarr/Test.Book"
                         }
                     }
                 });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.Warning);
        }

        private Sabnzbd CreateLegacyClient()
        {
            var legacy = new Sabnzbd(
                _proxy.Object,
                Mock.Of<IHttpClient>(),
                _configService.Object,
                Mock.Of<IDiskProvider>(),
                _remotePathMappingService.Object,
                Mock.Of<IValidateNzbs>(),
                LogManager.GetLogger("test"));

            legacy.Definition = new DownloadClientDefinition
            {
                Id = 2,
                Name = "TestSAB",
                Settings = new SabnzbdSettings
                {
                    Host = "127.0.0.1",
                    Port = 8080,
                    ApiKey = "abc",
                    MusicCategory = "Readarr"
                }
            };

            return legacy;
        }
    }
}
