using System;

namespace NzbDrone.Core.Download.Clients
{
    /// <summary>
    /// Flags representing the optional operations a download client may support.
    /// Adapters should report only the capabilities they actually implement so
    /// callers can branch at runtime without try/catch.
    /// </summary>
    [Flags]
    public enum DownloadClientCapabilities
    {
        None = 0,

        /// <summary>Can pause a queued or active download.</summary>
        CanPause = 1 << 0,

        /// <summary>Can resume a previously paused download.</summary>
        CanResume = 1 << 1,

        /// <summary>Can remove an item from the client queue.</summary>
        CanRemove = 1 << 2,

        /// <summary>Can set or change the client-side category/label.</summary>
        CanSetCategory = 1 << 3,

        /// <summary>Can set a per-torrent seed-ratio limit.</summary>
        CanSetSeedRatio = 1 << 4,

        /// <summary>Can set a per-torrent seed-time limit.</summary>
        CanSetSeedTime = 1 << 5,

        /// <summary>Can move a torrent to the top of the download queue.</summary>
        CanSetPriority = 1 << 6,

        /// <summary>Can add a torrent from a magnet URI.</summary>
        CanAddFromMagnet = 1 << 7,

        /// <summary>Can add a torrent from raw .torrent file bytes.</summary>
        CanAddFromFile = 1 << 8,

        /// <summary>Can add a release from an NZB file.</summary>
        CanAddNzb = 1 << 9,
    }
}
