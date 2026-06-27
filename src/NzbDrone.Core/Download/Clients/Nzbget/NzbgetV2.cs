namespace NzbDrone.Core.Download.Clients.Nzbget
{
    public class NzbgetV2 : LegacyDownloadClientV2Adapter<Nzbget>
    {
        public NzbgetV2(Nzbget legacyClient)
            : base(legacyClient)
        {
        }

        public override DownloadClientCapabilities Capabilities => DownloadClientCapabilities.CanRemove;
    }
}
