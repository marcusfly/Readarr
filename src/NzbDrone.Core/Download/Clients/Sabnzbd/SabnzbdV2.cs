namespace NzbDrone.Core.Download.Clients.Sabnzbd
{
    public class SabnzbdV2 : LegacyDownloadClientV2Adapter<Sabnzbd>
    {
        public SabnzbdV2(Sabnzbd legacyClient)
            : base(legacyClient)
        {
        }

        public override DownloadClientCapabilities Capabilities => DownloadClientCapabilities.CanRemove;
    }
}
