using System;

namespace NzbDrone.Core.Download.Clients.Transmission
{
    public class TransmissionV2 : LegacyDownloadClientV2Adapter<Transmission>
    {
        private readonly ITransmissionProxy _proxy;

        public TransmissionV2(Transmission legacyClient, ITransmissionProxy proxy)
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
            _proxy.SetTorrentSeedingConfiguration(downloadId.ToLower(), new TorrentSeedConfiguration { Ratio = ratio }, Settings);
        }

        public override void SetSeedTime(string downloadId, int minutes)
        {
            _proxy.SetTorrentSeedingConfiguration(downloadId.ToLower(), new TorrentSeedConfiguration { SeedTime = TimeSpan.FromMinutes(minutes) }, Settings);
        }

        protected TransmissionSettings Settings => (TransmissionSettings)LegacyClient.Definition.Settings;

        protected virtual string GetDownloadDirectory()
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
