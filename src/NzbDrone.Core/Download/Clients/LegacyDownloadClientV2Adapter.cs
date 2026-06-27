using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Download.Clients
{
    /// <summary>
    /// Bridges the legacy <see cref="IDownloadClient"/> contract onto the versioned
    /// <see cref="IDownloadClientV2"/> surface so existing adapters can opt into the
    /// normalised queue/capability model without rewriting their full implementation.
    /// </summary>
    public abstract class LegacyDownloadClientV2Adapter<TLegacy> : IDownloadClientV2
        where TLegacy : class, IDownloadClient
    {
        protected readonly TLegacy LegacyClient;

        protected LegacyDownloadClientV2Adapter(TLegacy legacyClient)
        {
            LegacyClient = legacyClient;
        }

        public string Name => LegacyClient.Name;

        public Type ConfigContract => LegacyClient.ConfigContract;

        public ProviderMessage Message => LegacyClient.Message;

        public IEnumerable<ProviderDefinition> DefaultDefinitions => LegacyClient.DefaultDefinitions;

        public ProviderDefinition Definition
        {
            get => LegacyClient.Definition;
            set => LegacyClient.Definition = value;
        }

        public ValidationResult Test()
        {
            return LegacyClient.Test();
        }

        public object RequestAction(string stage, IDictionary<string, string> query)
        {
            return LegacyClient.RequestAction(stage, query);
        }

        public DownloadProtocol Protocol => LegacyClient.Protocol;

        public abstract DownloadClientCapabilities Capabilities { get; }

        public virtual IEnumerable<DownloadQueueItem> GetQueue()
        {
            return LegacyClient.GetItems().Select(MapQueueItem);
        }

        public virtual string AddFromUrl(string url)
        {
            throw new NotSupportedException(Name + " does not support adding items from a URL via the v2 adapter");
        }

        public virtual void Remove(string downloadId, bool deleteData)
        {
            var item = LegacyClient.GetItems().FirstOrDefault(i => i.DownloadId == downloadId) ??
                       new DownloadClientItem
                       {
                           DownloadId = downloadId,
                           Status = DownloadItemStatus.Completed
                       };

            LegacyClient.RemoveItem(item, deleteData);
        }

        public virtual void TestConnectivity(List<ValidationFailure> failures)
        {
            failures.AddRange(LegacyClient.Test().Errors);
        }

        public virtual void Pause(string downloadId)
        {
            throw new NotSupportedException(Name + " does not support pausing items");
        }

        public virtual void Resume(string downloadId)
        {
            throw new NotSupportedException(Name + " does not support resuming items");
        }

        public virtual void SetCategory(string downloadId, string category)
        {
            throw new NotSupportedException(Name + " does not support setting categories");
        }

        public virtual void SetSeedRatio(string downloadId, double ratio)
        {
            throw new NotSupportedException(Name + " does not support per-item seed ratio limits");
        }

        public virtual void SetSeedTime(string downloadId, int minutes)
        {
            throw new NotSupportedException(Name + " does not support per-item seed time limits");
        }

        protected virtual DownloadQueueItem MapQueueItem(DownloadClientItem item)
        {
            return new DownloadQueueItem
            {
                DownloadId = item.DownloadId,
                Title = item.Title,
                State = MapState(item),
                TotalSize = item.TotalSize,
                RemainingSize = item.RemainingSize,
                RemainingTime = item.RemainingTime,
                SeedRatio = item.SeedRatio,
                OutputPath = item.OutputPath,
                Message = item.Message,
                Category = item.Category,
                CanMoveFiles = item.CanMoveFiles,
                CanBeRemoved = item.CanBeRemoved
            };
        }

        protected virtual DownloadQueueItemState MapState(DownloadClientItem item)
        {
            if (item.Removed)
            {
                return DownloadQueueItemState.Removed;
            }

            return item.Status switch
            {
                DownloadItemStatus.Queued => DownloadQueueItemState.Queued,
                DownloadItemStatus.Paused => DownloadQueueItemState.Paused,
                DownloadItemStatus.Downloading when item.RemainingSize == 0 => DownloadQueueItemState.PostProcessing,
                DownloadItemStatus.Downloading => DownloadQueueItemState.Downloading,
                DownloadItemStatus.Completed when Protocol == DownloadProtocol.Torrent && !item.CanBeRemoved => DownloadQueueItemState.Seeding,
                DownloadItemStatus.Completed => DownloadQueueItemState.Completed,
                DownloadItemStatus.Warning => DownloadQueueItemState.Warning,
                DownloadItemStatus.Failed => DownloadQueueItemState.Failed,
                _ => DownloadQueueItemState.Downloading
            };
        }
    }
}
