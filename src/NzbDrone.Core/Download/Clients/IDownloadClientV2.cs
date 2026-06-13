using System.Collections.Generic;
using FluentValidation.Results;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Download.Clients
{
    /// <summary>
    /// Versioned download-client contract.  Adapters that implement this interface
    /// declare their optional capabilities via <see cref="Capabilities"/> so callers
    /// can branch safely without catching <see cref="System.NotSupportedException"/>.
    /// </summary>
    /// <remarks>
    /// The existing <see cref="NzbDrone.Core.Download.IDownloadClient"/> is kept for
    /// backward compatibility.  New code should target <see cref="IDownloadClientV2"/>
    /// and new adapters should implement both interfaces until the old one is removed.
    /// </remarks>
    public interface IDownloadClientV2 : IProvider
    {
        /// <summary>Download protocol handled by this client (Torrent or Usenet).</summary>
        DownloadProtocol Protocol { get; }

        /// <summary>
        /// Bitmask of optional operations this adapter supports.
        /// Callers must check the relevant flag before invoking the corresponding method.
        /// </summary>
        DownloadClientCapabilities Capabilities { get; }

        // ── Core operations (always available) ────────────────────────────────────

        /// <summary>
        /// Returns a snapshot of the items currently managed by this client.
        /// </summary>
        IEnumerable<DownloadQueueItem> GetQueue();

        /// <summary>
        /// Adds a torrent or NZB by URL and returns the opaque download identifier.
        /// </summary>
        string AddFromUrl(string url);

        /// <summary>
        /// Removes the item identified by <paramref name="downloadId"/> from the
        /// client queue.  Requires <see cref="DownloadClientCapabilities.CanRemove"/>.
        /// </summary>
        void Remove(string downloadId, bool deleteData);

        /// <summary>
        /// Tests connectivity and configuration, populating <paramref name="failures"/>
        /// with any problems found.
        /// </summary>
        void TestConnectivity(List<ValidationFailure> failures);

        // ── Optional operations ────────────────────────────────────────────────────

        /// <summary>
        /// Pauses the download identified by <paramref name="downloadId"/>.
        /// Only valid when <see cref="DownloadClientCapabilities.CanPause"/> is set.
        /// </summary>
        void Pause(string downloadId);

        /// <summary>
        /// Resumes a paused download.
        /// Only valid when <see cref="DownloadClientCapabilities.CanResume"/> is set.
        /// </summary>
        void Resume(string downloadId);

        /// <summary>
        /// Sets the category/label on an existing queue item.
        /// Only valid when <see cref="DownloadClientCapabilities.CanSetCategory"/> is set.
        /// </summary>
        void SetCategory(string downloadId, string category);

        /// <summary>
        /// Sets the per-torrent seed ratio limit.
        /// Only valid when <see cref="DownloadClientCapabilities.CanSetSeedRatio"/> is set.
        /// </summary>
        void SetSeedRatio(string downloadId, double ratio);

        /// <summary>
        /// Sets the per-torrent seed time limit in minutes.
        /// Only valid when <see cref="DownloadClientCapabilities.CanSetSeedTime"/> is set.
        /// </summary>
        void SetSeedTime(string downloadId, int minutes);
    }
}
