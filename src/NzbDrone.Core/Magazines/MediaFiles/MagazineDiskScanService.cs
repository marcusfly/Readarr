using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Magazines.MediaFiles
{
    public interface IMagazineDiskScanService
    {
        void Scan(List<string> folders = null);
        List<IFileInfo> GetMagazineFiles(string path, bool allDirectories = true);
    }

    public class MagazineDiskScanService : IMagazineDiskScanService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMagazineRootFolderService _magazineRootFolderService;
        private readonly IMagazineService _magazineService;
        private readonly IMagazineFilenameParser _filenameParser;
        private readonly IMagazineImportDecisionMaker _decisionMaker;
        private readonly IImportApprovedMagazineIssues _importApprovedMagazineIssues;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public MagazineDiskScanService(IDiskProvider diskProvider,
                                       IMagazineRootFolderService magazineRootFolderService,
                                       IMagazineService magazineService,
                                       IMagazineFilenameParser filenameParser,
                                       IMagazineImportDecisionMaker decisionMaker,
                                       IImportApprovedMagazineIssues importApprovedMagazineIssues,
                                       IEventAggregator eventAggregator,
                                       Logger logger)
        {
            _diskProvider = diskProvider;
            _magazineRootFolderService = magazineRootFolderService;
            _magazineService = magazineService;
            _filenameParser = filenameParser;
            _decisionMaker = decisionMaker;
            _importApprovedMagazineIssues = importApprovedMagazineIssues;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public void Scan(List<string> folders = null)
        {
            var rootFolders = folders ?? _magazineRootFolderService.GetAll().Select(x => x.Path).ToList();
            var scannedMagazineIds = new HashSet<int>();

            foreach (var root in rootFolders)
            {
                if (!_diskProvider.FolderExists(root))
                {
                    _logger.Warn("Magazine root folder {0} does not exist, skipping scan.", root);
                    continue;
                }

                foreach (var folder in _diskProvider.GetDirectories(root))
                {
                    var folderName = Path.GetFileName(folder);
                    var folderInfo = _filenameParser.ParseFolderName(folderName);
                    var magazine = _magazineService.FindByNormalizedTitle(folderInfo.NormalizedMagazineTitle) ??
                                   _magazineService.FindByNormalizedTitle(folderName);

                    if (magazine == null)
                    {
                        _logger.Debug("Skipping unmanaged magazine folder {0}", folder);
                        continue;
                    }

                    var files = GetMagazineFiles(folder);
                    if (!files.Any())
                    {
                        continue;
                    }

                    var decisions = _decisionMaker.GetImportDecisions(files, magazine, folderInfo);
                    var rejected = decisions.Where(x => !x.Approved && x.LocalIssue?.ParsedInfo?.Confidence <= 0)
                                            .Select(x => x.LocalIssue.Path.FullName)
                                            .ToList();

                    foreach (var file in rejected)
                    {
                        _logger.Warn("Skipping magazine file {0} because it could not be parsed", file);
                    }

                    _importApprovedMagazineIssues.Import(decisions.Where(x => x.Approved).ToList(), false);
                    scannedMagazineIds.Add(magazine.Id);
                }
            }

            _eventAggregator.PublishEvent(new MagazineScanCompleteEvent(scannedMagazineIds.ToList(), false));
        }

        public List<IFileInfo> GetMagazineFiles(string path, bool allDirectories = true)
        {
            if (!_diskProvider.FolderExists(path))
            {
                return new List<IFileInfo>();
            }

            return _diskProvider.GetFiles(path, allDirectories)
                .Select(_diskProvider.GetFileInfo)
                .Where(file => file != null)
                .ToList();
        }
    }
}
