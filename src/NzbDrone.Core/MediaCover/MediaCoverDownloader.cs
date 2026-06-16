using System;
using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.MediaCover
{
    public class MediaCoverDownloader : IMediaCoverDownloader
    {
        private const string UserAgent = "Dalvik/2.1.0 (Linux; U; Android 10; SM-G975U Build/QP1A.190711.020)";

        private readonly IHttpClient _httpClient;
        private readonly IDiskProvider _diskProvider;
        private readonly ICoverExistsSpecification _coverExistsSpecification;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly Logger _logger;

        public MediaCoverDownloader(IHttpClient httpClient,
                                    IDiskProvider diskProvider,
                                    ICoverExistsSpecification coverExistsSpecification,
                                    IAppFolderInfo appFolderInfo,
                                    Logger logger)
        {
            _httpClient = httpClient;
            _diskProvider = diskProvider;
            _coverExistsSpecification = coverExistsSpecification;
            _appFolderInfo = appFolderInfo;
            _logger = logger;
        }

        public bool DownloadAuthorCoverIfNeeded(Author author, MediaCover cover)
        {
            var fileName = GetCoverPath(author.Id, MediaCoverEntity.Author, cover.CoverType, cover.Extension);
            var serverFileHeaders = GetServerHeaders(cover.Url);
            var alreadyExists = _coverExistsSpecification.AlreadyExists(serverFileHeaders.LastModified, GetContentLength(serverFileHeaders), fileName);

            if (!alreadyExists)
            {
                DownloadCover(fileName, cover.Url, cover.CoverType, author.ToString(), "author", serverFileHeaders.LastModified ?? DateTime.Now);
            }

            return !alreadyExists;
        }

        public bool DownloadBookCoverIfNeeded(Book book, MediaCover cover)
        {
            var fileName = GetCoverPath(book.Id, MediaCoverEntity.Book, cover.CoverType, cover.Extension);
            var serverFileHeaders = GetServerHeaders(cover.Url);
            var alreadyExists = _coverExistsSpecification.AlreadyExists(serverFileHeaders.LastModified, GetContentLength(serverFileHeaders), fileName);

            if (!alreadyExists)
            {
                DownloadCover(fileName, cover.Url, cover.CoverType, book.ToString(), "book", serverFileHeaders.LastModified ?? DateTime.Now);
            }

            return !alreadyExists;
        }

        private void DownloadCover(string fileName, string url, MediaCoverTypes coverType, string entityLabel, string entityKind, DateTime lastModified)
        {
            _logger.Info("Downloading {0} for {1} {2}", coverType, entityLabel, url);
            _httpClient.DownloadFile(url, fileName, UserAgent);

            try
            {
                _diskProvider.FileSetLastWriteTime(fileName, lastModified);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to set modified date for {0} image for {1} {2}", coverType, entityKind, entityLabel);
            }
        }

        private string GetCoverPath(int entityId, MediaCoverEntity coverEntity, MediaCoverTypes coverType, string extension)
        {
            var coverRootFolder = _appFolderInfo.GetMediaCoverPath();

            if (coverEntity == MediaCoverEntity.Book)
            {
                return Path.Combine(coverRootFolder, "Books", entityId.ToString(), coverType.ToString().ToLower() + GetExtension(coverType, extension));
            }

            return Path.Combine(coverRootFolder, entityId.ToString(), coverType.ToString().ToLower() + GetExtension(coverType, extension));
        }

        private string GetExtension(MediaCoverTypes coverType, string defaultExtension)
        {
            return coverType switch
            {
                MediaCoverTypes.Clearlogo => ".png",
                _ => defaultExtension
            };
        }

        private HttpHeader GetServerHeaders(string url)
        {
            // Goodreads doesn't allow a HEAD, so request a zero byte range instead
            var request = new HttpRequest(url)
            {
                AllowAutoRedirect = true
            };

            request.Headers.Add("Range", "bytes=0-0");
            request.Headers.Add("User-Agent", UserAgent);

            return _httpClient.Get(request).Headers;
        }

        private long? GetContentLength(HttpHeader headers)
        {
            var range = headers.Get("content-range");

            if (range == null)
            {
                return null;
            }

            var split = range.Split('/');
            if (split.Length == 2 && long.TryParse(split[1], out var length))
            {
                return length;
            }

            return null;
        }
    }
}
