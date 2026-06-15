using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Crypto;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Magazines.Services
{
    public interface IMagazineImportService
    {
        List<MagazineImportItem> GetMediaFiles(string path, MagazineIssue issue = null);
        List<MagazineImportItem> UpdateItems(List<MagazineImportItem> items);
    }

    public class MagazineImportService : IMagazineImportService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineIssueFileService _magazineIssueFileService;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MagazineImportService(IDiskProvider diskProvider,
                                     IMagazineIssueService magazineIssueService,
                                     IMagazineIssueFileService magazineIssueFileService,
                                     IEventAggregator eventAggregator,
                                     Logger logger)
        {
            _diskProvider = diskProvider;
            _magazineIssueService = magazineIssueService;
            _magazineIssueFileService = magazineIssueFileService;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<MagazineImportItem> GetMediaFiles(string path, MagazineIssue issue = null)
        {
            if (path.IsNullOrWhiteSpace())
            {
                return new List<MagazineImportItem>();
            }

            var files = new List<IFileInfo>();

            if (_diskProvider.FileExists(path))
            {
                files.Add(_diskProvider.GetFileInfo(path));
            }
            else if (_diskProvider.FolderExists(path))
            {
                files.AddRange(_diskProvider.GetFiles(path, true).Select(_diskProvider.GetFileInfo));
            }
            else
            {
                return new List<MagazineImportItem>();
            }

            return files.Select(fileInfo => MapItem(fileInfo, issue))
                        .Where(item => item != null)
                        .OrderBy(item => item.Path)
                        .ToList();
        }

        public List<MagazineImportItem> UpdateItems(List<MagazineImportItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return new List<MagazineImportItem>();
            }

            var result = new List<MagazineImportItem>();

            foreach (var item in items.Where(x => x != null))
            {
                var issue = _magazineIssueService.GetIssue(item.MagazineIssueId);

                if (issue == null)
                {
                    throw new NzbDrone.Core.Datastore.ModelNotFoundException(typeof(MagazineIssue), item.MagazineIssueId);
                }

                if (!_diskProvider.FileExists(item.Path))
                {
                    throw new FileNotFoundException($"Magazine file {item.Path} was not found", item.Path);
                }

                var fileInfo = _diskProvider.GetFileInfo(item.Path);
                var quality = item.Quality ?? MagazineFormatDetector.DetectQuality(fileInfo.Name);

                var magazineIssueFile = _magazineIssueFileService
                    .GetFilesForIssue(issue.Id)
                    .FirstOrDefault(x => PathEqualityComparer.Instance.Equals(x.Path, item.Path));

                var isNew = magazineIssueFile == null;

                if (isNew)
                {
                    magazineIssueFile = new MagazineIssueFile();
                }

                magazineIssueFile.MagazineIssueId = issue.Id;
                magazineIssueFile.MagazineId = issue.MagazineId;
                magazineIssueFile.Path = item.Path;
                magazineIssueFile.Size = fileInfo.Length;
                magazineIssueFile.DateAdded = isNew ? DateTime.UtcNow : magazineIssueFile.DateAdded;
                magazineIssueFile.Quality = quality;

                if (isNew)
                {
                    _magazineIssueFileService.Add(magazineIssueFile);
                }
                else
                {
                    _magazineIssueFileService.Update(magazineIssueFile);
                }

                _eventAggregator.PublishEvent(new MagazineIssueFileImportedEvent(issue, magazineIssueFile));

                result.Add(MapItem(_diskProvider.GetFileInfo(item.Path), issue, magazineIssueFile));
            }

            return result;
        }

        private MagazineImportItem MapItem(IFileInfo fileInfo, MagazineIssue issue = null, MagazineIssueFile existingFile = null)
        {
            var quality = existingFile?.Quality ?? MagazineFormatDetector.DetectQuality(fileInfo.Name);

            if (quality?.Quality == Quality.Unknown && existingFile == null)
            {
                _logger.Debug("Skipping non-magazine file {0}", fileInfo.FullName);
                return null;
            }

            var targetIssue = issue ?? (existingFile != null ? _magazineIssueService.GetIssue(existingFile.MagazineIssueId) : null);

            return new MagazineImportItem
            {
                Id = HashConverter.GetHashInt31(fileInfo.FullName),
                Path = fileInfo.FullName,
                Name = fileInfo.Name,
                Size = fileInfo.Length,
                MagazineId = targetIssue?.MagazineId ?? existingFile?.MagazineId ?? 0,
                MagazineIssueId = targetIssue?.Id ?? existingFile?.MagazineIssueId ?? 0,
                IssueYear = targetIssue?.IssueYear ?? 0,
                IssueMonth = targetIssue?.IssueMonth ?? 0,
                IssueDay = targetIssue?.IssueDay,
                Volume = targetIssue?.Volume,
                IssueNumber = targetIssue?.IssueNumber,
                ReleaseTitle = targetIssue?.ReleaseTitle,
                Quality = quality,
                Rejections = Array.Empty<Rejection>()
            };
        }
    }

    public class MagazineImportItem
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public int MagazineId { get; set; }
        public int MagazineIssueId { get; set; }
        public int IssueYear { get; set; }
        public int IssueMonth { get; set; }
        public int? IssueDay { get; set; }
        public string Volume { get; set; }
        public string IssueNumber { get; set; }
        public string ReleaseTitle { get; set; }
        public QualityModel Quality { get; set; }
        public IEnumerable<Rejection> Rejections { get; set; }
    }
}
