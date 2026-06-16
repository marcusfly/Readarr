using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.ContentTypes;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.TagExtraction
{
    public class TagExtractionService : ITagExtractionService
    {
        private const int Capacity = 500;

        private readonly IAudioTagService _audioTagService;
        private readonly IEBookTagService _ebookTagService;
        private readonly IDiskProvider _diskProvider;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cache = new Dictionary<string, LinkedListNode<CacheEntry>>();
        private readonly Dictionary<string, HashSet<string>> _pathKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<CacheEntry> _lru = new LinkedList<CacheEntry>();

        public TagExtractionService(IAudioTagService audioTagService,
                                    IEBookTagService ebookTagService,
                                    IDiskProvider diskProvider,
                                    Logger logger)
        {
            _audioTagService = audioTagService;
            _ebookTagService = ebookTagService;
            _diskProvider = diskProvider;
        }

        public FileTagResult GetTags(string path)
        {
            var fileInfo = _diskProvider.GetFileInfo(path);
            var canonicalPath = CanonicalizePath(fileInfo.FullName);
            var cacheKey = BuildCacheKey(canonicalPath, fileInfo.LastWriteTimeUtc.Ticks);

            lock (_syncRoot)
            {
                if (_cache.TryGetValue(cacheKey, out var existing))
                {
                    _lru.Remove(existing);
                    _lru.AddFirst(existing);
                    return existing.Value.Result;
                }
            }

            var result = ReadTags(fileInfo);

            lock (_syncRoot)
            {
                if (_cache.TryGetValue(cacheKey, out var existing))
                {
                    _lru.Remove(existing);
                    _lru.AddFirst(existing);
                    return existing.Value.Result;
                }

                var entry = new CacheEntry(canonicalPath, cacheKey, result);
                var node = new LinkedListNode<CacheEntry>(entry);
                _lru.AddFirst(node);
                _cache[cacheKey] = node;

                if (!_pathKeys.TryGetValue(canonicalPath, out var keys))
                {
                    keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _pathKeys[canonicalPath] = keys;
                }

                keys.Add(cacheKey);

                while (_cache.Count > Capacity)
                {
                    RemoveNode(_lru.Last);
                }
            }

            return result;
        }

        public void Evict(string path)
        {
            var canonicalPath = CanonicalizePath(path);

            lock (_syncRoot)
            {
                if (!_pathKeys.TryGetValue(canonicalPath, out var keys))
                {
                    return;
                }

                foreach (var key in keys.ToList())
                {
                    if (_cache.TryGetValue(key, out var node))
                    {
                        RemoveNode(node);
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_syncRoot)
            {
                _cache.Clear();
                _pathKeys.Clear();
                _lru.Clear();
            }
        }

        private FileTagResult ReadTags(IFileInfo fileInfo)
        {
            var extension = Path.GetExtension(fileInfo.FullName);
            var contentType = MediaFileExtensions.GetContentTypeForExtension(extension);

            ParsedTrackInfo parsed;

            if (contentType == LibraryContentType.Audiobook)
            {
                parsed = _audioTagService.ReadTags(fileInfo.FullName);
            }
            else
            {
                parsed = _ebookTagService.ReadTags(fileInfo);
                contentType = contentType == LibraryContentType.None ? LibraryContentType.Book : contentType;
            }

            parsed ??= new ParsedTrackInfo();

            return new FileTagResult
            {
                Title = parsed.BookTitle ?? parsed.Title,
                Author = parsed.Authors.FirstOrDefault(),
                Narrator = null,
                Series = parsed.SeriesTitle,
                SeriesIndex = parsed.SeriesIndex,
                Publisher = parsed.Publisher ?? parsed.Label,
                Year = parsed.Year > 0 ? (int?)parsed.Year : null,
                Isbn = parsed.Isbn,
                Asin = parsed.Asin,
                Quality = parsed.Quality ?? new Qualities.QualityModel(Qualities.Quality.Unknown),
                ContentType = contentType,
                HasChapters = false, // TODO: expose chapter metadata once TagLib parsing is available in ParsedTrackInfo.
                ChapterCount = null,
                DurationMs = parsed.Duration > TimeSpan.Zero ? (long?)parsed.Duration.TotalMilliseconds : null,
                ParsedTrackInfo = parsed
            };
        }

        private string CanonicalizePath(string path)
        {
            return Path.GetFullPath(path ?? string.Empty);
        }

        private static string BuildCacheKey(string canonicalPath, long lastWriteTicks)
        {
            return canonicalPath + "|" + lastWriteTicks;
        }

        private void RemoveNode(LinkedListNode<CacheEntry> node)
        {
            if (node == null)
            {
                return;
            }

            _lru.Remove(node);
            _cache.Remove(node.Value.CacheKey);

            if (_pathKeys.TryGetValue(node.Value.CanonicalPath, out var keys))
            {
                keys.Remove(node.Value.CacheKey);
                if (keys.Count == 0)
                {
                    _pathKeys.Remove(node.Value.CanonicalPath);
                }
            }
        }

        private sealed class CacheEntry
        {
            public CacheEntry(string canonicalPath, string cacheKey, FileTagResult result)
            {
                CanonicalPath = canonicalPath;
                CacheKey = cacheKey;
                Result = result;
            }

            public string CanonicalPath { get; }
            public string CacheKey { get; }
            public FileTagResult Result { get; }
        }
    }
}
