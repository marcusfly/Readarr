using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Events;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.MediaCover
{
    public interface IMapCoversToLocal
    {
        void ConvertToLocalUrls(int entityId, MediaCoverEntity coverEntity, IEnumerable<MediaCover> covers);
        string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes coverType, string extension, int? height = null);
        void EnsureBookCovers(Book book);
    }

    public class MediaCoverService :
        IHandleAsync<AuthorRefreshCompleteEvent>,
        IHandleAsync<AuthorDeletedEvent>,
        IHandleAsync<BookDeletedEvent>,
        IHandleAsync<MagazineDeletedEvent>,
        IMapCoversToLocal
    {
        private readonly IMediaCoverProxy _mediaCoverProxy;
        private readonly IImageResizer _resizer;
        private readonly IBookService _bookService;
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigFileProvider _configFileProvider;
        private readonly IMediaCoverDownloader _coverDownloader;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDeferredCoverService _deferredCoverService;
        private readonly Logger _logger;

        private readonly string _coverRootFolder;

        // ImageSharp is slow on ARM (no hardware acceleration on mono yet)
        // So limit the number of concurrent resizing tasks
        private static readonly SemaphoreSlim Semaphore = new SemaphoreSlim((int)Math.Ceiling(Environment.ProcessorCount / 2.0));

        public MediaCoverService(IMediaCoverProxy mediaCoverProxy,
                                 IImageResizer resizer,
                                 IBookService bookService,
                                 IDiskProvider diskProvider,
                                 IAppFolderInfo appFolderInfo,
                                 IConfigFileProvider configFileProvider,
                                 IMediaCoverDownloader coverDownloader,
                                 IDeferredCoverService deferredCoverService,
                                 IEventAggregator eventAggregator,
                                 Logger logger)
        {
            _mediaCoverProxy = mediaCoverProxy;
            _resizer = resizer;
            _bookService = bookService;
            _diskProvider = diskProvider;
            _configFileProvider = configFileProvider;
            _coverDownloader = coverDownloader;
            _deferredCoverService = deferredCoverService;
            _eventAggregator = eventAggregator;
            _logger = logger;

            _coverRootFolder = appFolderInfo.GetMediaCoverPath();
        }

        public string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes coverType, string extension, int? height = null)
        {
            var heightSuffix = height.HasValue ? "-" + height.ToString() : "";

            if (coverEntity == MediaCoverEntity.Book)
            {
                return Path.Combine(GetBookCoverPath(entityId), coverType.ToString().ToLower() + heightSuffix + GetExtension(coverType, extension));
            }

            if (coverEntity == MediaCoverEntity.Magazine)
            {
                return Path.Combine(GetMagazineCoverPath(entityId), coverType.ToString().ToLower() + heightSuffix + GetExtension(coverType, extension));
            }

            if (coverEntity == MediaCoverEntity.MagazineIssue)
            {
                return Path.Combine(GetMagazineIssueCoverPath(entityId), coverType.ToString().ToLower() + heightSuffix + GetExtension(coverType, extension));
            }

            return Path.Combine(GetAuthorCoverPath(entityId), coverType.ToString().ToLower() + heightSuffix + GetExtension(coverType, extension));
        }

        public void ConvertToLocalUrls(int entityId, MediaCoverEntity coverEntity, IEnumerable<MediaCover> covers)
        {
            if (entityId == 0)
            {
                // Author isn't in Readarr yet, map via a proxy to circument referrer issues
                foreach (var mediaCover in covers)
                {
                    mediaCover.RemoteUrl = mediaCover.Url;
                    mediaCover.Url = _mediaCoverProxy.RegisterUrl(mediaCover.RemoteUrl);
                }
            }
            else
            {
                foreach (var mediaCover in covers)
                {
                    if (mediaCover.CoverType == MediaCoverTypes.Unknown)
                    {
                        continue;
                    }

                    var filePath = GetCoverPath(entityId, coverEntity, mediaCover.CoverType, mediaCover.Extension, null);

                    mediaCover.RemoteUrl = mediaCover.Url;

                    if (coverEntity == MediaCoverEntity.Book)
                    {
                        mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/Books/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + GetExtension(mediaCover.CoverType, mediaCover.Extension);
                    }
                    else if (coverEntity == MediaCoverEntity.Magazine)
                    {
                        mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/Magazines/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + GetExtension(mediaCover.CoverType, mediaCover.Extension);
                    }
                    else if (coverEntity == MediaCoverEntity.MagazineIssue)
                    {
                        mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/MagazineIssues/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + GetExtension(mediaCover.CoverType, mediaCover.Extension);
                    }
                    else
                    {
                        mediaCover.Url = _configFileProvider.UrlBase + @"/MediaCover/" + entityId + "/" + mediaCover.CoverType.ToString().ToLower() + GetExtension(mediaCover.CoverType, mediaCover.Extension);
                    }

                    if (_diskProvider.FileExists(filePath))
                    {
                        var lastWrite = _diskProvider.FileGetLastWrite(filePath);
                        mediaCover.Url += "?lastWrite=" + lastWrite.Ticks;
                    }
                }
            }
        }

        private string GetAuthorCoverPath(int authorId)
        {
            return Path.Combine(_coverRootFolder, authorId.ToString());
        }

        private string GetBookCoverPath(int bookId)
        {
            return Path.Combine(_coverRootFolder, "Books", bookId.ToString());
        }

        private string GetMagazineCoverPath(int magazineId)
        {
            return Path.Combine(_coverRootFolder, "Magazines", magazineId.ToString());
        }

        private string GetMagazineIssueCoverPath(int issueId)
        {
            return Path.Combine(_coverRootFolder, "MagazineIssues", issueId.ToString());
        }

        private void EnsureAuthorCovers(Author author)
        {
            var toResize = new List<Tuple<MediaCover, bool>>();

            foreach (var cover in author.Metadata.Value.Images)
            {
                if (cover.CoverType == MediaCoverTypes.Unknown)
                {
                    continue;
                }

                var fileName = GetCoverPath(author.Id, MediaCoverEntity.Author, cover.CoverType, cover.Extension);
                var alreadyExists = false;

                try
                {
                    alreadyExists = !_coverDownloader.DownloadAuthorCoverIfNeeded(author, cover);
                }
                catch (HttpException e)
                {
                    _logger.Warn("Couldn't download media cover for {0}. {1}", author, e.Message);
                }
                catch (WebException e)
                {
                    _logger.Warn("Couldn't download media cover for {0}. {1}", author, e.Message);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't download media cover for {0}", author);
                }

                toResize.Add(Tuple.Create(cover, alreadyExists));
            }

            try
            {
                Semaphore.Wait();

                foreach (var tuple in toResize)
                {
                    EnsureResizedCovers(author, tuple.Item1, !tuple.Item2);
                }
            }
            finally
            {
                Semaphore.Release();
            }
        }

        public void EnsureBookCovers(Book book)
        {
            foreach (var cover in book.GetBestMonitoredEdition()?.Images.Where(e => e.CoverType == MediaCoverTypes.Cover) ?? Enumerable.Empty<MediaCover>())
            {
                if (cover.CoverType == MediaCoverTypes.Unknown)
                {
                    continue;
                }

                var fileName = GetCoverPath(book.Id, MediaCoverEntity.Book, cover.CoverType, cover.Extension, null);
                var alreadyExists = false;

                try
                {
                    alreadyExists = !_coverDownloader.DownloadBookCoverIfNeeded(book, cover);
                }
                catch (HttpException e)
                {
                    _logger.Warn("Couldn't download media cover for {0}. {1}", book, e.Message);
                }
                catch (WebException e)
                {
                    _logger.Warn("Couldn't download media cover for {0}. {1}", book, e.Message);
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Couldn't download media cover for {0}", book);
                }
            }
        }

        public bool DownloadAuthorCoverIfNeeded(Author author, MediaCover cover)
        {
            var fileName = GetCoverPath(author.Id, MediaCoverEntity.Author, cover.CoverType, cover.Extension);
            var alreadyExists = false;

            alreadyExists = !_coverDownloader.DownloadAuthorCoverIfNeeded(author, cover);

            try
            {
                Semaphore.Wait();
                EnsureResizedCovers(author, cover, !alreadyExists);
            }
            finally
            {
                Semaphore.Release();
            }

            return !alreadyExists;
        }

        public bool DownloadBookCoverIfNeeded(Book book, MediaCover cover)
        {
            return _coverDownloader.DownloadBookCoverIfNeeded(book, cover);
        }

        private void EnsureResizedCovers(Author author, MediaCover cover, bool forceResize, Book book = null)
        {
            var heights = GetDefaultHeights(cover.CoverType);

            foreach (var height in heights)
            {
                var mainFileName = GetCoverPath(author.Id, MediaCoverEntity.Author, cover.CoverType, cover.Extension);
                var resizeFileName = GetCoverPath(author.Id, MediaCoverEntity.Author, cover.CoverType, cover.Extension, height);

                if (forceResize || !_diskProvider.FileExists(resizeFileName) || _diskProvider.GetFileSize(resizeFileName) == 0)
                {
                    _logger.Debug("Resizing {0}-{1} for {2}", cover.CoverType, height, author);

                    try
                    {
                        _resizer.Resize(mainFileName, resizeFileName, height);
                    }
                    catch
                    {
                        _logger.Debug("Couldn't resize media cover {0}-{1} for author {2}, using full size image instead.", cover.CoverType, height, author);
                    }
                }
            }
        }

        private int[] GetDefaultHeights(MediaCoverTypes coverType)
        {
            switch (coverType)
            {
                default:
                    return new int[] { };

                case MediaCoverTypes.Poster:
                case MediaCoverTypes.Disc:
                case MediaCoverTypes.Cover:
                case MediaCoverTypes.Logo:
                case MediaCoverTypes.Headshot:
                    return new[] { 500, 250 };

                case MediaCoverTypes.Banner:
                    return new[] { 70, 35 };

                case MediaCoverTypes.Fanart:
                case MediaCoverTypes.Screenshot:
                    return new[] { 360, 180 };
            }
        }

        private string GetExtension(MediaCoverTypes coverType, string defaultExtension)
        {
            return coverType switch
            {
                MediaCoverTypes.Clearlogo => ".png",
                _ => defaultExtension
            };
        }

        public void HandleAsync(AuthorRefreshCompleteEvent message)
        {
            var books = _bookService.GetBooksByAuthor(message.Author.Id);
            _deferredCoverService.EnqueueAll(message.Author.Id, message.Author.Metadata.Value.Images
                                                               .Where(cover => cover.CoverType != MediaCoverTypes.Unknown)
                                                               .Select(cover => (cover.CoverType, cover.Url)));

            foreach (var book in books)
            {
                _deferredCoverService.EnqueueAll(message.Author.Id, book.GetBestMonitoredEdition()?.Images
                                                                  .Where(cover => cover.CoverType == MediaCoverTypes.Cover)
                                                                  .Select(cover => (cover.CoverType, cover.Url)) ??
                                                                  Enumerable.Empty<(MediaCoverTypes, string)>());
            }
        }

        public void HandleAsync(AuthorDeletedEvent message)
        {
            var path = GetAuthorCoverPath(message.Author.Id);
            if (_diskProvider.FolderExists(path))
            {
                _diskProvider.DeleteFolder(path, true);
            }
        }

        public void HandleAsync(BookDeletedEvent message)
        {
            var path = GetBookCoverPath(message.Book.Id);
            if (_diskProvider.FolderExists(path))
            {
                _diskProvider.DeleteFolder(path, true);
            }
        }

        public void HandleAsync(MagazineDeletedEvent message)
        {
            var path = GetMagazineCoverPath(message.Magazine.Id);
            if (_diskProvider.FolderExists(path))
            {
                _diskProvider.DeleteFolder(path, true);
            }
        }
    }
}
