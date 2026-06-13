using System;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Download.Clients
{
    /// <summary>
    /// Normalized queue state that all download-client adapters must map their
    /// native states onto.  Callers should not depend on <see cref="DownloadItemStatus"/>
    /// for new code — use <see cref="DownloadQueueItemState"/> instead.
    /// </summary>
    public enum DownloadQueueItemState
    {
        /// <summary>Item is waiting to start.</summary>
        Queued,

        /// <summary>Item is actively being downloaded.</summary>
        Downloading,

        /// <summary>Item has finished downloading and is now seeding (torrents).</summary>
        Seeding,

        /// <summary>Item has been paused by the user or client.</summary>
        Paused,

        /// <summary>Item has finished successfully and is ready for import.</summary>
        Completed,

        /// <summary>A non-recoverable error has occurred.</summary>
        Failed,

        /// <summary>
        /// Item was removed from the client queue externally (e.g. manually deleted
        /// in the client UI) before Readarr could process it.
        /// </summary>
        Removed,
    }

    /// <summary>
    /// Normalized representation of a single item in a download client's queue.
    /// Produced by <see cref="IDownloadClientV2.GetQueue"/> and consumed by the
    /// Completed/Failed Download Handling pipeline.
    /// </summary>
    public class DownloadQueueItem
    {
        /// <summary>Opaque identifier assigned by the download client (e.g. torrent hash, NZB ID).</summary>
        public string DownloadId { get; set; }

        /// <summary>Display name of the release.</summary>
        public string Title { get; set; }

        /// <summary>Normalised lifecycle state.</summary>
        public DownloadQueueItemState State { get; set; }

        /// <summary>Total size of the release in bytes.</summary>
        public long TotalSize { get; set; }

        /// <summary>Bytes still to be transferred.</summary>
        public long RemainingSize { get; set; }

        /// <summary>Estimated time remaining, or <c>null</c> if unknown.</summary>
        public TimeSpan? RemainingTime { get; set; }

        /// <summary>Download speed in bytes/second, or <c>null</c> if unavailable.</summary>
        public long? DownloadSpeed { get; set; }

        /// <summary>
        /// Current seed ratio (bytes uploaded / bytes downloaded).
        /// <c>null</c> for Usenet or when not applicable.
        /// </summary>
        public double? SeedRatio { get; set; }

        /// <summary>Path where downloaded files reside, if known.</summary>
        public OsPath OutputPath { get; set; }

        /// <summary>Human-readable message from the client (errors, warnings, etc.).</summary>
        public string Message { get; set; }

        /// <summary>Client-side category or label.</summary>
        public string Category { get; set; }

        /// <summary>
        /// <c>true</c> when the files may be moved/renamed by Readarr
        /// (i.e. the item is completed and no seed/ratio limits remain active).
        /// </summary>
        public bool CanMoveFiles { get; set; }

        /// <summary>
        /// <c>true</c> when Readarr may instruct the client to remove the item.
        /// </summary>
        public bool CanBeRemoved { get; set; }
    }
}
