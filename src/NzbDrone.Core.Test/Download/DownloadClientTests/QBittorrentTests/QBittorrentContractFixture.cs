using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.QBittorrent;

namespace NzbDrone.Core.Test.Download.DownloadClientTests.QBittorrentTests
{
    /// <summary>
    /// Concrete instantiation of <see cref="DownloadClientContractTests"/> for
    /// the <see cref="QBittorrentV2"/> adapter.
    ///
    /// All tests defined in the abstract base run here automatically.  This file
    /// only adds the qBittorrent-specific wiring (mock proxy, settings, helpers).
    /// </summary>
    [TestFixture]
    public class QBittorrentContractFixture : DownloadClientContractTests
    {
        private Mock<IQBittorrentProxySelector> _proxySelector;
        private Mock<IQBittorrentProxy> _proxy;

        [SetUp]
        public void Setup()
        {
            _proxy = new Mock<IQBittorrentProxy>(MockBehavior.Strict);
            _proxySelector = new Mock<IQBittorrentProxySelector>(MockBehavior.Strict);

            _proxySelector
                .Setup(s => s.GetProxy(It.IsAny<QBittorrentSettings>(), It.IsAny<bool>()))
                .Returns(_proxy.Object);

            _proxySelector
                .Setup(s => s.GetApiVersion(It.IsAny<QBittorrentSettings>(), It.IsAny<bool>()))
                .Returns(new Version(2, 9, 0));
        }

        protected override IDownloadClientV2 CreateSubject()
        {
            var subject = new QBittorrentV2(_proxySelector.Object, LogManager.GetLogger("test"));
            subject.Definition = new DownloadClientDefinition
            {
                Id = 1,
                Name = "TestQBittorrent",
                Settings = new QBittorrentSettings
                {
                    Host = "127.0.0.1",
                    Port = 8080,
                    Username = "admin",
                    Password = "password",
                    MusicCategory = "music",
                }
            };
            return subject;
        }

        protected override void GivenEmptyQueue()
        {
            _proxy
                .Setup(p => p.GetConfig(It.IsAny<QBittorrentSettings>()))
                .Returns(new QBittorrentPreferences { DhtEnabled = true });

            _proxy
                .Setup(p => p.GetTorrents(It.IsAny<QBittorrentSettings>()))
                .Returns(new List<QBittorrentTorrent>());
        }

        protected override void GivenAuthenticationFailure()
        {
            _proxySelector
                .Setup(s => s.GetProxy(It.IsAny<QBittorrentSettings>(), It.IsAny<bool>()))
                .Returns(_proxy.Object);

            _proxy
                .Setup(p => p.GetApiVersion(It.IsAny<QBittorrentSettings>()))
                .Throws(new DownloadClientAuthenticationException("Unauthorized"));
        }

        // ── qBittorrent-specific supplemental tests ────────────────────────────────
        [Test]
        public void GetQueue_should_map_downloading_state_correctly()
        {
            _proxy
                .Setup(p => p.GetConfig(It.IsAny<QBittorrentSettings>()))
                .Returns(new QBittorrentPreferences { DhtEnabled = true });

            _proxy
                .Setup(p => p.GetTorrents(It.IsAny<QBittorrentSettings>()))
                .Returns(new List<QBittorrentTorrent>
                {
                    new QBittorrentTorrent
                    {
                        Hash = "aabbcc",
                        Name = "Test.Book",
                        State = "downloading",
                        Size = 100_000_000,
                        Progress = 0.5f,
                        Ratio = 0,
                        Eta = 600,
                    }
                });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.Downloading);
            items[0].DownloadId.Should().Be("AABBCC");
        }

        [Test]
        public void GetQueue_should_map_seeding_state_correctly()
        {
            _proxy
                .Setup(p => p.GetConfig(It.IsAny<QBittorrentSettings>()))
                .Returns(new QBittorrentPreferences { DhtEnabled = true });

            _proxy
                .Setup(p => p.GetTorrents(It.IsAny<QBittorrentSettings>()))
                .Returns(new List<QBittorrentTorrent>
                {
                    new QBittorrentTorrent
                    {
                        Hash = "ddeeff",
                        Name = "Test.Book",
                        State = "uploading",
                        Size = 100_000_000,
                        Progress = 1.0f,
                        Ratio = 0.5f,
                        Eta = 8640000,
                        RatioLimit = -1,
                        SeedingTimeLimit = -1,
                        InactiveSeedingTimeLimit = -1,
                    }
                });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.Seeding);
        }

        [Test]
        public void GetQueue_should_map_paused_state_correctly()
        {
            _proxy
                .Setup(p => p.GetConfig(It.IsAny<QBittorrentSettings>()))
                .Returns(new QBittorrentPreferences { DhtEnabled = true });

            _proxy
                .Setup(p => p.GetTorrents(It.IsAny<QBittorrentSettings>()))
                .Returns(new List<QBittorrentTorrent>
                {
                    new QBittorrentTorrent
                    {
                        Hash = "112233",
                        Name = "Test.Book",
                        State = "pausedDL",
                        Size = 100_000_000,
                        Progress = 0.3f,
                        Ratio = 0,
                        Eta = -1,
                    }
                });

            var items = new List<DownloadQueueItem>(CreateSubject().GetQueue());

            items.Should().HaveCount(1);
            items[0].State.Should().Be(DownloadQueueItemState.Paused);
        }

        [Test]
        public void Capabilities_should_include_expected_torrent_flags()
        {
            var caps = CreateSubject().Capabilities;

            caps.HasFlag(DownloadClientCapabilities.CanPause).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanResume).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanRemove).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanSetCategory).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanSetSeedRatio).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanAddFromMagnet).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanAddFromFile).Should().BeTrue();
            caps.HasFlag(DownloadClientCapabilities.CanAddNzb).Should().BeFalse();
        }
    }
}
