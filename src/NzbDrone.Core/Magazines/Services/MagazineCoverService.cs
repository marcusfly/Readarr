using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.MediaCover;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using MediaCoverModel = NzbDrone.Core.MediaCover.MediaCover;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineCoverService
    {
        List<MediaCoverModel> GetImages(Magazine magazine, IEnumerable<MagazineIssue> issues);
        List<MediaCoverModel> GetIssueImages(MagazineIssue issue);
    }

    public class MagazineCoverService : IMagazineCoverService
    {
        private const string AuthoritySourceFileName = "cover.url";
        private static readonly string[] ArchiveImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp" };
        private readonly IDiskProvider _diskProvider;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IMapCoversToLocal _coverMapper;
        private readonly IMagazineTitleAuthorityProvider _titleAuthorityProvider;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        public MagazineCoverService(IDiskProvider diskProvider,
                                    IAppFolderInfo appFolderInfo,
                                    IMapCoversToLocal coverMapper,
                                    IMagazineTitleAuthorityProvider titleAuthorityProvider,
                                    IHttpClient httpClient,
                                    Logger logger)
        {
            _diskProvider = diskProvider;
            _appFolderInfo = appFolderInfo;
            _coverMapper = coverMapper;
            _titleAuthorityProvider = titleAuthorityProvider;
            _httpClient = httpClient;
            _logger = logger;
        }

        public List<MediaCoverModel> GetImages(Magazine magazine, IEnumerable<MagazineIssue> issues)
        {
            if (magazine == null || magazine.Id <= 0)
            {
                return new List<MediaCoverModel>();
            }

            var issueFiles = issues?
                .SelectMany(issue => issue?.IssueFiles?.Value ?? new List<MagazineIssueFile>())
                .Where(file => file != null && !file.Path.IsNullOrWhiteSpace())
                .OrderByDescending(file => file.DateAdded)
                .ToList() ?? new List<MagazineIssueFile>();

            var cover = issueFiles.Any()
                ? EnsureCover(magazine, issueFiles)
                : EnsureAuthorityCover(magazine);

            if (cover == null)
            {
                return new List<MediaCoverModel>();
            }

            var covers = new List<MediaCoverModel> { cover };
            _coverMapper.ConvertToLocalUrls(magazine.Id, MediaCoverEntity.Magazine, covers);

            return covers;
        }

        public List<MediaCoverModel> GetIssueImages(MagazineIssue issue)
        {
            if (issue == null || issue.Id <= 0)
            {
                return new List<MediaCoverModel>();
            }

            var issueFiles = issue.IssueFiles?.Value?
                .Where(file => file != null && !file.Path.IsNullOrWhiteSpace())
                .OrderByDescending(file => file.DateAdded)
                .ToList() ?? new List<MagazineIssueFile>();

            if (!issueFiles.Any())
            {
                return new List<MediaCoverModel>();
            }

            var cover = EnsureIssueCover(issue, issueFiles);
            if (cover == null)
            {
                return new List<MediaCoverModel>();
            }

            var covers = new List<MediaCoverModel> { cover };
            _coverMapper.ConvertToLocalUrls(issue.Id, MediaCoverEntity.MagazineIssue, covers);

            return covers;
        }

        private MediaCoverModel EnsureCover(Magazine magazine, List<MagazineIssueFile> issueFiles)
        {
            var sourceFile = issueFiles.FirstOrDefault(file => _diskProvider.FileExists(file.Path) && IsSupported(file.Path));
            if (sourceFile == null)
            {
                return null;
            }

            var coverPath = _coverMapper.GetCoverPath(magazine.Id, MediaCoverEntity.Magazine, MediaCoverTypes.Cover, ".jpg", null);
            var coverDirectory = Path.GetDirectoryName(coverPath);

            if (!coverDirectory.IsNullOrWhiteSpace())
            {
                _diskProvider.CreateFolder(coverDirectory);
            }

            if (_diskProvider.FileExists(coverPath) &&
                _diskProvider.GetFileSize(coverPath) > 0 &&
                _diskProvider.FileGetLastWrite(coverPath) >= _diskProvider.FileGetLastWrite(sourceFile.Path))
            {
                return new MediaCoverModel(MediaCoverTypes.Cover, "cover.jpg");
            }

            try
            {
                GenerateCover(sourceFile.Path, coverPath);
                _diskProvider.FileSetLastWriteTime(coverPath, _diskProvider.FileGetLastWrite(sourceFile.Path));
                return new MediaCoverModel(MediaCoverTypes.Cover, "cover.jpg");
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to generate magazine cover from {0}", sourceFile.Path);
                return null;
            }
        }

        private MediaCoverModel EnsureAuthorityCover(Magazine magazine)
        {
            try
            {
                var authority = _titleAuthorityProvider.LookupByTitleAsync(magazine.Title).GetAwaiter().GetResult();
                var remoteUrl = authority?.ImageUrl ?? authority?.LogoUrl;
                if (remoteUrl.IsNullOrWhiteSpace())
                {
                    return FindExistingMagazineCover(magazine.Id);
                }

                var extension = GetRemoteExtension(remoteUrl);
                var coverPath = _coverMapper.GetCoverPath(magazine.Id, MediaCoverEntity.Magazine, MediaCoverTypes.Cover, extension, null);
                var sourcePath = GetAuthoritySourcePath(coverPath);
                var coverDirectory = Path.GetDirectoryName(coverPath);

                if (!coverDirectory.IsNullOrWhiteSpace())
                {
                    _diskProvider.CreateFolder(coverDirectory);
                }

                var shouldRefresh = !_diskProvider.FileExists(coverPath) ||
                                    _diskProvider.GetFileSize(coverPath) == 0 ||
                                    !AuthoritySourceMatches(sourcePath, remoteUrl);

                if (shouldRefresh)
                {
                    _logger.Info("Downloading authority cover for magazine {0} from {1}", magazine.Title, remoteUrl);
                    _httpClient.DownloadFile(remoteUrl, coverPath);
                    _diskProvider.WriteAllText(sourcePath, remoteUrl);
                    DeleteStaleMagazineCovers(magazine.Id, extension);
                }

                return new MediaCoverModel(MediaCoverTypes.Cover, remoteUrl);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to download authority cover for magazine {0}", magazine.Title);
                return FindExistingMagazineCover(magazine.Id);
            }
        }

        private MediaCoverModel FindExistingMagazineCover(int magazineId)
        {
            foreach (var extension in ArchiveImageExtensions.Concat(new[] { ".svg" }).Distinct())
            {
                var coverPath = _coverMapper.GetCoverPath(magazineId, MediaCoverEntity.Magazine, MediaCoverTypes.Cover, extension, null);
                if (_diskProvider.FileExists(coverPath) && _diskProvider.GetFileSize(coverPath) > 0)
                {
                    return new MediaCoverModel(MediaCoverTypes.Cover, "cover" + extension);
                }
            }

            return null;
        }

        private static string GetRemoteExtension(string remoteUrl)
        {
            try
            {
                var path = Uri.UnescapeDataString(new Uri(remoteUrl).AbsolutePath);
                var extension = Path.GetExtension(path);

                if (extension.IsNotNullOrWhiteSpace())
                {
                    return extension.ToLowerInvariant();
                }
            }
            catch
            {
            }

            return ".jpg";
        }

        private static string GetAuthoritySourcePath(string coverPath)
        {
            var directory = Path.GetDirectoryName(coverPath) ?? string.Empty;
            return Path.Combine(directory, AuthoritySourceFileName);
        }

        private bool AuthoritySourceMatches(string sourcePath, string remoteUrl)
        {
            if (!_diskProvider.FileExists(sourcePath))
            {
                return false;
            }

            return _diskProvider.ReadAllText(sourcePath).Trim().Equals(remoteUrl, StringComparison.OrdinalIgnoreCase);
        }

        private void DeleteStaleMagazineCovers(int magazineId, string keepExtension)
        {
            foreach (var extension in ArchiveImageExtensions.Concat(new[] { ".svg" }).Distinct())
            {
                if (extension.Equals(keepExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var coverPath = _coverMapper.GetCoverPath(magazineId, MediaCoverEntity.Magazine, MediaCoverTypes.Cover, extension, null);
                if (_diskProvider.FileExists(coverPath))
                {
                    _diskProvider.DeleteFile(coverPath);
                }
            }
        }

        private MediaCoverModel EnsureIssueCover(MagazineIssue issue, List<MagazineIssueFile> issueFiles)
        {
            var sourceFile = issueFiles.FirstOrDefault(file => _diskProvider.FileExists(file.Path) && IsSupported(file.Path));
            if (sourceFile == null)
            {
                return null;
            }

            var coverPath = _coverMapper.GetCoverPath(issue.Id, MediaCoverEntity.MagazineIssue, MediaCoverTypes.Cover, ".jpg", null);
            var coverDirectory = Path.GetDirectoryName(coverPath);

            if (!coverDirectory.IsNullOrWhiteSpace())
            {
                _diskProvider.CreateFolder(coverDirectory);
            }

            if (_diskProvider.FileExists(coverPath) &&
                _diskProvider.GetFileSize(coverPath) > 0 &&
                _diskProvider.FileGetLastWrite(coverPath) >= _diskProvider.FileGetLastWrite(sourceFile.Path))
            {
                return new MediaCoverModel(MediaCoverTypes.Cover, "cover.jpg");
            }

            try
            {
                GenerateCover(sourceFile.Path, coverPath);
                _diskProvider.FileSetLastWriteTime(coverPath, _diskProvider.FileGetLastWrite(sourceFile.Path));
                return new MediaCoverModel(MediaCoverTypes.Cover, "cover.jpg");
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to generate magazine issue cover from {0}", sourceFile.Path);
                return null;
            }
        }

        private void GenerateCover(string sourcePath, string destinationPath)
        {
            var extension = Path.GetExtension(sourcePath)?.ToLowerInvariant();

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".webp":
                case ".gif":
                case ".bmp":
                    using (var stream = _diskProvider.OpenReadStream(sourcePath))
                    {
                        SaveNormalizedCover(stream, destinationPath);
                    }

                    return;

                case ".cbz":
                    using (var stream = _diskProvider.OpenReadStream(sourcePath))
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                    {
                        using var imageStream = OpenArchiveImageStream(archive);
                        SaveNormalizedCover(imageStream, destinationPath);
                    }

                    return;

                case ".epub":
                case ".kepub":
                    using (var stream = _diskProvider.OpenReadStream(sourcePath))
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false))
                    {
                        using var imageStream = OpenArchiveImageStream(archive);
                        SaveNormalizedCover(imageStream, destinationPath);
                    }

                    return;

                case ".pdf":
                    RenderPdfCover(sourcePath, destinationPath);
                    return;

                default:
                    throw new NotSupportedException($"Magazine cover extraction does not support '{extension}' files.");
            }
        }

        private static bool IsSupported(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();

            return extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp" or ".cbz" or ".epub" or ".kepub" or ".pdf";
        }

        private static Stream OpenArchiveImageStream(ZipArchive archive)
        {
            var entry = archive.Entries
                .Where(item => !item.FullName.EndsWith("/", StringComparison.Ordinal) && ArchiveImageExtensions.Contains(Path.GetExtension(item.FullName).ToLowerInvariant()))
                .OrderByDescending(item => item.FullName.Contains("cover", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (entry == null)
            {
                throw new InvalidOperationException("No image entry was found in the archive.");
            }

            return entry.Open();
        }

        private static void SaveNormalizedCover(Stream sourceStream, string destinationPath)
        {
            using var image = Image.Load(sourceStream);

            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(320, 480)
            }));

            image.SaveAsJpeg(destinationPath, new JpegEncoder
            {
                Quality = 85
            });
        }

        private void RenderPdfCover(string sourcePath, string destinationPath)
        {
            var tempBase = Path.Combine(_appFolderInfo.TempFolder, $"readarr-magazine-cover-{Guid.NewGuid():N}");
            var renderedPath = tempBase + ".jpg";

            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "pdftoppm",
                    Arguments = $"-jpeg -singlefile -f 1 -l 1 \"{sourcePath}\" \"{tempBase}\"",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                process?.WaitForExit();

                if (process == null || process.ExitCode != 0 || !_diskProvider.FileExists(renderedPath))
                {
                    var error = process?.StandardError.ReadToEnd();
                    throw new InvalidOperationException($"pdftoppm failed to render magazine cover. {error}");
                }

                using var renderedStream = _diskProvider.OpenReadStream(renderedPath);
                SaveNormalizedCover(renderedStream, destinationPath);
            }
            finally
            {
                if (_diskProvider.FileExists(renderedPath))
                {
                    _diskProvider.DeleteFile(renderedPath);
                }
            }
        }
    }
}
