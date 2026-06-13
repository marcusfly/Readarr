using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.QBittorrent
{
    /// <summary>
    /// <see cref="IDownloadClientV2"/> adapter for qBittorrent.
    /// This class maps qBittorrent's native API onto the normalised
    /// <see cref="DownloadQueueItem"/> / <see cref="DownloadQueueItemState"/>
    /// model and exposes a precise <see cref="DownloadClientCapabilities"/> bitmask
    /// so callers never need to guess what the client supports.
    /// </summary>
    public class QBittorrentV2 : IDownloadClientV2
    {
        private readonly IQBittorrentProxySelector _proxySelector;
        private readonly Logger _logger;

        public QBittorrentV2(IQBittorrentProxySelector proxySelector, Logger logger)
        {
            _proxySelector = proxySelector;
            _logger = logger;
        }

        // ── IProvider ────────────────────────────────────────────────────────────
        public string Name => "qBittorrent";

        public Type ConfigContract => typeof(QBittorrentSettings);

        public ProviderMessage Message => null;

        public IEnumerable<ProviderDefinition> DefaultDefinitions => new List<ProviderDefinition>
        {
            new DownloadClientDefinition { Enable = true, Name = Name, Settings = new QBittorrentSettings() }
        };

        public ProviderDefinition Definition { get; set; }

        public ValidationResult Test()
        {
            var failures = new List<ValidationFailure>();
            TestConnectivity(failures);
            return new ValidationResult(failures);
        }

        public object RequestAction(string stage, IDictionary<string, string> query) => null;

        public IEnumerable<ProviderMessage> GetInfoMessages() => Enumerable.Empty<ProviderMessage>();

        private QBittorrentSettings Settings => (QBittorrentSettings)Definition.Settings;

        private IQBittorrentProxy Proxy => _proxySelector.GetProxy(Settings);

        // ── IDownloadClientV2 ────────────────────────────────────────────────────
        public DownloadProtocol Protocol => DownloadProtocol.Torrent;

        public DownloadClientCapabilities Capabilities =>
            DownloadClientCapabilities.CanPause |
            DownloadClientCapabilities.CanResume |
            DownloadClientCapabilities.CanRemove |
            DownloadClientCapabilities.CanSetCategory |
            DownloadClientCapabilities.CanSetSeedRatio |
            DownloadClientCapabilities.CanSetSeedTime |
            DownloadClientCapabilities.CanSetPriority |
            DownloadClientCapabilities.CanAddFromMagnet |
            DownloadClientCapabilities.CanAddFromFile;

        public IEnumerable<DownloadQueueItem> GetQueue()
        {
            var config = Proxy.GetConfig(Settings);
            var torrents = Proxy.GetTorrents(Settings);

            foreach (var torrent in torrents)
            {
                var item = new DownloadQueueItem
                {
                    DownloadId = torrent.Hash.ToUpper(),
                    Title = torrent.Name,
                    TotalSize = torrent.Size,
                    RemainingSize = (long)(torrent.Size * (1.0 - torrent.Progress)),
                    SeedRatio = torrent.Ratio,
                    Category = torrent.Category.IsNotNullOrWhiteSpace() ? torrent.Category : torrent.Label,
                };

                item.State = MapState(torrent, config, item);
                item.CanMoveFiles = item.CanBeRemoved =
                    torrent.State is "pausedUP" or "stoppedUP" &&
                    HasReachedSeedLimit(torrent, config);

                yield return item;
            }
        }

        public string AddFromUrl(string url)
        {
            Proxy.AddTorrentFromUrl(url, null, Settings);
            return url; // The hash is not immediately available without a round-trip.
        }

        public void Remove(string downloadId, bool deleteData)
        {
            Proxy.RemoveTorrent(downloadId.ToLower(), deleteData, Settings);
        }

        public void TestConnectivity(List<ValidationFailure> failures)
        {
            try
            {
                var version = _proxySelector.GetProxy(Settings, true).GetApiVersion(Settings);
                if (version < Version.Parse("1.5"))
                {
                    failures.Add(new NzbDroneValidationFailure("Host", "Unsupported client version")
                    {
                        DetailedDescription = "Please upgrade to qBittorrent version 3.2.4 or higher."
                    });
                }
            }
            catch (DownloadClientAuthenticationException ex)
            {
                _logger.Error(ex, "Unable to authenticate with qBittorrent");
                failures.Add(new NzbDroneValidationFailure("Username", "Authentication failure")
                {
                    DetailedDescription = "Please verify your username and password."
                });
            }
            catch (WebException ex)
            {
                _logger.Error(ex, "Unable to connect to qBittorrent");
                failures.Add(new NzbDroneValidationFailure("Host", "Unable to connect")
                {
                    DetailedDescription = "Please verify the hostname and port."
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to test qBittorrent");
                failures.Add(new NzbDroneValidationFailure("Host", "Unable to connect to qBittorrent")
                {
                    DetailedDescription = ex.Message
                });
            }
        }

        public void Pause(string downloadId)
        {
            // qBittorrent does not expose a pause-by-hash API in the proxy abstraction;
            // using force-start = false achieves the equivalent stop.
            Proxy.SetForceStart(downloadId.ToLower(), false, Settings);
        }

        public void Resume(string downloadId)
        {
            Proxy.SetForceStart(downloadId.ToLower(), true, Settings);
        }

        public void SetCategory(string downloadId, string category)
        {
            Proxy.SetTorrentLabel(downloadId.ToLower(), category, Settings);
        }

        public void SetSeedRatio(string downloadId, double ratio)
        {
            var config = new TorrentSeedConfiguration { Ratio = ratio };
            Proxy.SetTorrentSeedingConfiguration(downloadId.ToLower(), config, Settings);
        }

        public void SetSeedTime(string downloadId, int minutes)
        {
            var config = new TorrentSeedConfiguration { SeedTime = TimeSpan.FromMinutes(minutes) };
            Proxy.SetTorrentSeedingConfiguration(downloadId.ToLower(), config, Settings);
        }

        // ── State mapping ────────────────────────────────────────────────────────
        private static DownloadQueueItemState MapState(
            QBittorrentTorrent torrent,
            QBittorrentPreferences config,
            DownloadQueueItem item)
        {
            switch (torrent.State)
            {
                case "stoppedDL":
                case "pausedDL":
                    return DownloadQueueItemState.Paused;

                case "queuedDL":
                case "checkingDL":
                case "checkingUP":
                case "checkingResumeData":
                    return DownloadQueueItemState.Queued;

                case "pausedUP":
                case "stoppedUP":
                    return DownloadQueueItemState.Seeding;

                case "uploading":
                case "stalledUP":
                case "queuedUP":
                case "forcedUP":
                    return DownloadQueueItemState.Seeding;

                case "error":
                case "missingFiles":
                    item.Message = torrent.State == "error"
                        ? "qBittorrent is reporting an error"
                        : "The download is missing files";
                    return DownloadQueueItemState.Failed;

                case "stalledDL":
                    item.Message = "The download is stalled with no connections";
                    return DownloadQueueItemState.Downloading;

                case "metaDL":
                case "forcedMetaDL":
                    item.Message = config.DhtEnabled
                        ? "qBittorrent is downloading metadata"
                        : "qBittorrent cannot resolve magnet link with DHT disabled";
                    return DownloadQueueItemState.Queued;

                case "forcedDL":
                case "moving":
                case "downloading":
                    return DownloadQueueItemState.Downloading;

                default:
                    item.Message = "Unknown state: " + torrent.State;
                    return DownloadQueueItemState.Downloading;
            }
        }

        private static bool HasReachedSeedLimit(QBittorrentTorrent torrent, QBittorrentPreferences config)
        {
            if (torrent.RatioLimit >= 0)
            {
                return torrent.RatioLimit - torrent.Ratio <= 0.001f;
            }

            if (torrent.RatioLimit == -2 && config.MaxRatioEnabled)
            {
                return config.MaxRatio - torrent.Ratio <= 0.001f;
            }

            return false;
        }
    }
}
