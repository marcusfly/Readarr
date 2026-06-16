using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaCover
{
    public interface IDeferredCoverClock
    {
        DateTime UtcNow { get; }
    }

    public class DeferredCoverClock : IDeferredCoverClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public interface IMediaCoverDownloader
    {
        bool DownloadAuthorCoverIfNeeded(Author author, MediaCover cover);
        bool DownloadBookCoverIfNeeded(Book book, MediaCover cover);
    }

    public class DeferredCoverDownloadService : IDeferredCoverService, IExecute<ProcessDeferredCoversCommand>
    {
        private const int MaxItemsPerExecution = 50;
        private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

        private readonly ConcurrentQueue<DeferredCoverItem> _queue = new ConcurrentQueue<DeferredCoverItem>();
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IMediaCoverDownloader _coverDownloader;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeferredCoverClock _clock;
        private readonly Logger _logger;
        private int _pendingCount;

        public DeferredCoverDownloadService(IAuthorService authorService,
                                            IBookService bookService,
                                            IMediaCoverDownloader coverDownloader,
                                            IEventAggregator eventAggregator,
                                            IDeferredCoverClock clock,
                                            Logger logger)
        {
            _authorService = authorService;
            _bookService = bookService;
            _coverDownloader = coverDownloader;
            _eventAggregator = eventAggregator;
            _clock = clock;
            _logger = logger;
        }

        public int PendingCount => Volatile.Read(ref _pendingCount);

        public void Enqueue(int authorId, MediaCoverTypes coverType, string url)
        {
            if (authorId <= 0 || url.IsNullOrWhiteSpace())
            {
                return;
            }

            _queue.Enqueue(new DeferredCoverItem(authorId, coverType, url, _clock.UtcNow));
            Interlocked.Increment(ref _pendingCount);
        }

        public void EnqueueAll(int authorId, IEnumerable<(MediaCoverTypes type, string url)> covers)
        {
            if (covers == null)
            {
                return;
            }

            foreach (var cover in covers)
            {
                Enqueue(authorId, cover.type, cover.url);
            }
        }

        public void Execute(ProcessDeferredCoversCommand message)
        {
            var updatedAuthors = new HashSet<int>();

            for (var i = 0; i < MaxItemsPerExecution; i++)
            {
                if (!_queue.TryDequeue(out var item))
                {
                    break;
                }

                Interlocked.Decrement(ref _pendingCount);

                if (_clock.UtcNow - item.EnqueuedAt > MaxAge)
                {
                    _logger.Debug("Dropping stale deferred cover item for author {0}: {1}", item.AuthorId, item.Url);
                    continue;
                }

                if (ProcessItem(item))
                {
                    updatedAuthors.Add(item.AuthorId);
                }
            }

            foreach (var authorId in updatedAuthors)
            {
                try
                {
                    _eventAggregator.PublishEvent(new MediaCoversUpdatedEvent(_authorService.GetAuthor(authorId)));
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unable to publish cover update event for author {0}", authorId);
                }
            }
        }

        private bool ProcessItem(DeferredCoverItem item)
        {
            Author author;

            try
            {
                author = _authorService.GetAuthor(item.AuthorId);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to resolve author {0} for deferred cover {1}", item.AuthorId, item.Url);
                return false;
            }

            var authorCover = author.Metadata?.Value?.Images?.FirstOrDefault(cover => CoverMatches(cover, item));
            if (authorCover != null)
            {
                return _coverDownloader.DownloadAuthorCoverIfNeeded(author, authorCover);
            }

            foreach (var book in _bookService.GetBooksByAuthor(item.AuthorId))
            {
                var bookCover = book.GetBestMonitoredEdition()?.Images?.FirstOrDefault(cover => CoverMatches(cover, item));
                if (bookCover != null)
                {
                    return _coverDownloader.DownloadBookCoverIfNeeded(book, bookCover);
                }
            }

            _logger.Debug("No matching cover metadata found for deferred item {0} / {1}", item.AuthorId, item.Url);
            return false;
        }

        private static bool CoverMatches(MediaCover cover, DeferredCoverItem item)
        {
            return cover != null &&
                   cover.CoverType == item.CoverType &&
                   cover.Url.Equals(item.Url, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class DeferredCoverItem
        {
            public DeferredCoverItem(int authorId, MediaCoverTypes coverType, string url, DateTime enqueuedAt)
            {
                AuthorId = authorId;
                CoverType = coverType;
                Url = url;
                EnqueuedAt = enqueuedAt;
            }

            public int AuthorId { get; }
            public MediaCoverTypes CoverType { get; }
            public string Url { get; }
            public DateTime EnqueuedAt { get; }
        }
    }
}
