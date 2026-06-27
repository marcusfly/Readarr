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
using NzbDrone.Core.Download.Clients.Nzbget;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.NzbgetTests
{
    [TestFixture]
    public class NzbgetContractFixture : DownloadClientContractTests
    {
        private Mock<INzbgetProxy> _proxy;
        private Mock<IRemotePathMappingService> _remotePathMappingService;
        private Mock<IConfigService> _configService;

        [SetUp]
        public void Setup()
        {
            _proxy = new Mock<INzbgetProxy>(MockBehavior.Strict);
            _remotePathMappingService = new Mock<IRemotePathMappingService>(MockBehavior.Loose);
            _remotePathMappingService
                .Setup(s => s.RemapRemoteToLocal(It.IsAny<string>(), It.IsAny<OsPath>()))
                .Returns((string _, OsPath path) => path);

            _configService = new Mock<IConfigService>(MockBehavior.Loose);
            _configService.SetupGet(s => s.DownloadClientHistoryLimit).Returns(30);
        }

        protected override IDownloadClientV2 CreateSubject()
        {
            return new NzbgetV2(CreateLegacyClient());
        }

        protected override void GivenEmptyQueue()
        {
            _proxy.Setup(p => p.GetGlobalStatus(It.IsAny<NzbgetSettings>()))
                 .Returns(new NzbgetGlobalStatus());
            _proxy.Setup(p => p.GetQueue(It.IsAny<NzbgetSettings>()))
                 .Returns(new List<NzbgetQueueItem>());
            _proxy.Setup(p => p.GetHistory(It.IsAny<NzbgetSettings>()))
                 .Returns(new List<NzbgetHistoryItem>());
        }

        protected override void GivenAuthenticationFailure()
        {
            _proxy.Setup(p => p.GetVersion(It.IsAny<NzbgetSettings>()))
                 .Throws(new System.Exception("Authentication failed"));
            _proxy.Setup(p => p.GetConfig(It.IsAny<NzbgetSettings>()))
                 .Returns(new Dictionary<string, string>
                 {
                     ["Category1.Name"] = "Readarr",
                     ["Category1.DestDir"] = "/downloads/Readarr",
                     ["KeepHistory"] = "10"
                 });
        }

        [Test]
        public void GetQueue_should_map_unpack_space_history_to_warning()
        {
            _proxy.Setup(p => p.GetGlobalStatus(It.IsAny<NzbgetSettings>()))
                 .Returns(new NzbgetGlobalStatus());
            _proxy.Setup(p => p.GetQueue(It.IsAny<NzbgetSettings>()))
                 .Returns(new List<NzbgetQueueItem>());
            _proxy.Setup(p => p.GetHistory(It.IsAny<NzbgetSettings>()))
                 .Returns(new List<NzbgetHistoryItem>
                 {
                     new NzbgetHistoryItem
                     {
                         Id = 11,
                         Name = "Test.Book",
                         Category = "Readarr",
                         UnpackStatus = "SPACE",
                         ParStatus = "SUCCESS",
                         MoveStatus = "SUCCESS",
                         ScriptStatus = "SUCCESS",
                         DeleteStatus = "NONE",
                         MarkStatus = "NONE",
                         DestDir = "/downloads/Readarr/Test.Book",
                         Parameters = new List<NzbgetParameter>()
                     }
                 });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.Warning);
        }

        private Nzbget CreateLegacyClient()
        {
            var legacy = new Nzbget(
                _proxy.Object,
                Mock.Of<IHttpClient>(),
                _configService.Object,
                Mock.Of<IDiskProvider>(),
                _remotePathMappingService.Object,
                Mock.Of<IValidateNzbs>(),
                LogManager.GetLogger("test"));

            legacy.Definition = new DownloadClientDefinition
            {
                Id = 3,
                Name = "TestNZBGet",
                Settings = new NzbgetSettings
                {
                    Host = "127.0.0.1",
                    Port = 6789,
                    Username = "nzbget",
                    Password = "secret",
                    MusicCategory = "Readarr"
                }
            };

            return legacy;
        }
    }
}
