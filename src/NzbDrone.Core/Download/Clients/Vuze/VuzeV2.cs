using System;
using NzbDrone.Core.Download.Clients.Transmission;

namespace NzbDrone.Core.Download.Clients.Vuze
{
    public class VuzeV2 : LegacyDownloadClientV2Adapter<Vuze>
    {
        private readonly ITransmissionProxy _proxy;

        public VuzeV2(Vuze legacyClient, ITransmissionProxy proxy)
            : base(legacyClient)
        {
            _proxy = proxy;
        }

        public override DownloadClientCapabilities Capabilities =>
            DownloadClientCapabilities.CanRemove |
            DownloadClientCapabilities.CanSetSeedRatio |
            DownloadClientCapabilities.CanSetSeedTime |
            DownloadClientCapabilities.CanAddFromMagnet;

        public override string AddFromUrl(string url)
        {
            _proxy.AddTorrentFromUrl(url, GetDownloadDirectory(), Settings);
            return url;
        }

        public override void SetSeedRatio(string downloadId, double ratio)
        {
            _proxy.SetTorrentSeedingConfiguration(downloadId, new TorrentSeedConfiguration { Ratio = ratio }, Settings);
        }

        public override void SetSeedTime(string downloadId, int minutes)
        {
            _proxy.SetTorrentSeedingConfiguration(downloadId, new TorrentSeedConfiguration { SeedTime = TimeSpan.FromMinutes(minutes) }, Settings);
        }

        private TransmissionSettings Settings => (TransmissionSettings)LegacyClient.Definition.Settings;

        private string GetDownloadDirectory()
        {
            if (!string.IsNullOrWhiteSpace(Settings.TvDirectory))
            {
                return Settings.TvDirectory;
            }

            if (string.IsNullOrWhiteSpace(Settings.MusicCategory))
            {
                return null;
            }

            var config = _proxy.GetConfig(Settings);
            return $"{config.DownloadDir.TrimEnd('/')}/{Settings.MusicCategory}";
        }
    }
}
