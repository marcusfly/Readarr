using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.MediaFiles;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.Magazines.Parser;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Services
{
    public class RescanMagazineService : IExecute<RescanMagazineCommand>
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMagazineService _magazineService;
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineRootFolderService _magazineRootFolderService;
        private readonly IMagazineDiskScanService _magazineDiskScanService;
        private readonly IAddMagazineService _addMagazineService;
        private readonly IMagazineTitleAuthorityProvider _titleAuthorityProvider;
        private readonly IIssnLTableImporter _issnLTableImporter;
        private readonly IMagazineFilenameParser _magazineFilenameParser;
        private readonly IMagazineMonitoredService _magazineMonitoredService;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IProcessDownloadDecisions _processDownloadDecisions;
        private readonly Logger _logger;

        public RescanMagazineService(IDiskProvider diskProvider,
                                     IMagazineService magazineService,
                                     IMagazineIssueService magazineIssueService,
                                     IMagazineRootFolderService magazineRootFolderService,
                                     IMagazineDiskScanService magazineDiskScanService,
                                     IAddMagazineService addMagazineService,
                                     IMagazineTitleAuthorityProvider titleAuthorityProvider,
                                     IIssnLTableImporter issnLTableImporter,
                                     IMagazineFilenameParser magazineFilenameParser,
                                     IMagazineMonitoredService magazineMonitoredService,
                                     ISearchForReleases releaseSearchService,
                                     IProcessDownloadDecisions processDownloadDecisions,
                                     Logger logger)
        {
            _diskProvider = diskProvider;
            _magazineService = magazineService;
            _magazineIssueService = magazineIssueService;
            _magazineRootFolderService = magazineRootFolderService;
            _magazineDiskScanService = magazineDiskScanService;
            _addMagazineService = addMagazineService;
            _titleAuthorityProvider = titleAuthorityProvider;
            _issnLTableImporter = issnLTableImporter;
            _magazineFilenameParser = magazineFilenameParser;
            _magazineMonitoredService = magazineMonitoredService;
            _releaseSearchService = releaseSearchService;
            _processDownloadDecisions = processDownloadDecisions;
            _logger = logger;
        }

        public void Execute(RescanMagazineCommand message)
        {
            if (message == null)
            {
                return;
            }

            var requestedMagazineIds = message.MagazineIds ?? new List<int>();
            var rootFolders = _magazineRootFolderService.GetAll();
            var discoveredMagazines = message.AddNewMagazines
                ? DiscoverMagazines(rootFolders)
                : new List<Magazine>();

            var magazineIds = requestedMagazineIds.Count > 0
                ? requestedMagazineIds
                : GetMagazinesForRootFolders(rootFolders)
                    .Select(x => x.Id)
                    .Concat(discoveredMagazines.Select(x => x.Id))
                    .Distinct()
                    .ToList();

            RefreshAuthorityMetadata(magazineIds);

            if (magazineIds.Count == 0)
            {
                _logger.Warn("Magazine rescan requested with no magazine ids or configured root-folder matches.");
                return;
            }

            _logger.Info("Rescanning {0} magazines. AddNewMagazines={1} AddNewIssues={2}", magazineIds.Count, message.AddNewMagazines, message.AddNewIssues);

            var scanFolders = GetScanFolders(rootFolders, requestedMagazineIds);

            if (scanFolders.Count > 0)
            {
                _magazineDiskScanService.Scan(scanFolders);
            }

            foreach (var magazine in discoveredMagazines)
            {
                _magazineMonitoredService.SetIssueMonitoredStatus(magazine, magazine.AddOptions?.Monitor ?? MonitorTypes.All);
            }

            if (!message.AddNewIssues)
            {
                return;
            }

            foreach (var magazineId in magazineIds)
            {
                var magazine = _magazineService.GetMagazine(magazineId);
                if (magazine == null)
                {
                    continue;
                }

                var issues = _magazineIssueService.GetMissingIssues(magazineId);
                if (issues.Count == 0)
                {
                    _logger.Info("No missing magazine issues found for '{0}'.", magazine.Title);
                    continue;
                }

                _logger.ProgressInfo("Searching for {0} missing issues for '{1}'", issues.Count, magazine.Title);

                foreach (var issue in issues)
                {
                    try
                    {
                        var decisions = _releaseSearchService.MagazineIssueSearch(issue.Id, false, false).GetAwaiter().GetResult();
                        var processed = _processDownloadDecisions.ProcessDecisions(decisions).GetAwaiter().GetResult();

                        _logger.ProgressInfo("Magazine rescan completed for {0}. {1} reports downloaded.", issue, processed.Grabbed.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Unable to rescan magazine issue: [{0}]", issue);
                    }
                }
            }
        }

        private List<string> GetScanFolders(List<MagazineRootFolder> rootFolders, List<int> requestedMagazineIds)
        {
            if (requestedMagazineIds.Count == 0)
            {
                return rootFolders.Select(x => x.Path)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();
            }

            var scanFolders = new HashSet<string>();

            foreach (var magazineId in requestedMagazineIds)
            {
                var magazine = _magazineService.GetMagazine(magazineId);
                if (magazine == null)
                {
                    _logger.Warn("Magazine with id {0} not found for rescan.", magazineId);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(magazine.Path))
                {
                    continue;
                }

                var rootFolder = Path.GetDirectoryName(magazine.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                if (!string.IsNullOrWhiteSpace(rootFolder))
                {
                    scanFolders.Add(rootFolder);
                }
            }

            return scanFolders.ToList();
        }

        private List<Magazine> DiscoverMagazines(List<MagazineRootFolder> rootFolders)
        {
            var discovered = new List<Magazine>();

            foreach (var rootFolder in rootFolders)
            {
                if (string.IsNullOrWhiteSpace(rootFolder.Path) || !_diskProvider.FolderExists(rootFolder.Path))
                {
                    continue;
                }

                foreach (var folder in _diskProvider.GetDirectories(rootFolder.Path))
                {
                    var folderName = Path.GetFileName(folder);
                    var parsedFolder = _magazineFilenameParser.ParseFolderName(folderName);
                    var existing = _magazineService.FindByNormalizedTitle(parsedFolder.NormalizedMagazineTitle) ??
                                   _magazineService.FindByNormalizedTitle(folderName);

                    if (existing != null)
                    {
                        continue;
                    }

                    var magazine = new Magazine
                    {
                        Title = parsedFolder.MagazineTitle,
                        Monitored = rootFolder.DefaultMonitorOption != MonitorTypes.None,
                        Path = folder,
                        RootFolderPath = rootFolder.Path,
                        QualityProfileId = rootFolder.DefaultQualityProfileId,
                        MetadataProfileId = rootFolder.DefaultMetadataProfileId,
                        Tags = rootFolder.DefaultTags != null ? new HashSet<int>(rootFolder.DefaultTags) : new HashSet<int>(),
                        AddOptions = new AddMagazineOptions
                        {
                            Monitor = rootFolder.DefaultMonitorOption,
                            SearchForMissingIssues = false
                        }
                    };

                    try
                    {
                        discovered.Add(_addMagazineService.AddMagazine(magazine));
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Unable to add magazine from root folder {0}", folder);
                    }
                }
            }

            return discovered;
        }

        private List<Magazine> GetMagazinesForRootFolders(List<MagazineRootFolder> rootFolders)
        {
            var rootFolderPaths = rootFolders.Select(x => x.Path)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (rootFolderPaths.Count == 0)
            {
                return new List<Magazine>();
            }

            return _magazineService.GetAllMagazines()
                .Where(magazine => !string.IsNullOrWhiteSpace(magazine.Path) &&
                                   rootFolderPaths.Any(root =>
                                       (!string.IsNullOrWhiteSpace(magazine.RootFolderPath) && root.PathEquals(magazine.RootFolderPath)) ||
                                       root.IsParentPath(magazine.Path)))
                .ToList();
        }

        private void RefreshAuthorityMetadata(IEnumerable<int> magazineIds)
        {
            foreach (var magazineId in magazineIds.Distinct())
            {
                var magazine = _magazineService.GetMagazine(magazineId);
                if (magazine == null || !NeedsAuthorityRefresh(magazine))
                {
                    continue;
                }

                try
                {
                    var authorityResult = _titleAuthorityProvider.LookupByTitleAsync(magazine.Title).GetAwaiter().GetResult();
                    if (!ApplyAuthorityBackfill(magazine, authorityResult))
                    {
                        continue;
                    }

                    _magazineService.UpdateMagazine(magazine);
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Unable to refresh authority metadata for magazine '{0}'", magazine.Title);
                }
            }
        }

        private bool NeedsAuthorityRefresh(Magazine magazine)
        {
            if (magazine == null)
            {
                return false;
            }

            if (magazine.Issn.IsNullOrWhiteSpace() || magazine.WikidataId.IsNullOrWhiteSpace())
            {
                return true;
            }

            return magazine.IssnL.IsNullOrWhiteSpace() && _issnLTableImporter?.GetIssnL(magazine.Issn).IsNotNullOrWhiteSpace() == true;
        }

        private bool ApplyAuthorityBackfill(Magazine magazine, MagazineAuthorityResult authorityResult)
        {
            if (magazine == null || authorityResult == null)
            {
                return false;
            }

            var changed = false;

            if (magazine.WikidataId.IsNullOrWhiteSpace() && authorityResult.WikidataId.IsNotNullOrWhiteSpace())
            {
                magazine.WikidataId = authorityResult.WikidataId;
                changed = true;
            }

            if (magazine.Issn.IsNullOrWhiteSpace() && authorityResult.Issn.IsNotNullOrWhiteSpace())
            {
                magazine.Issn = authorityResult.Issn;
                changed = true;
            }

            var resolvedIssnL = authorityResult.IssnL.IsNotNullOrWhiteSpace()
                ? authorityResult.IssnL
                : _issnLTableImporter?.GetIssnL(authorityResult.Issn ?? magazine.Issn);

            if (magazine.IssnL.IsNullOrWhiteSpace() && resolvedIssnL.IsNotNullOrWhiteSpace())
            {
                magazine.IssnL = resolvedIssnL;
                changed = true;
            }

            if (magazine.Country.IsNullOrWhiteSpace() && authorityResult.Country.IsNotNullOrWhiteSpace())
            {
                magazine.Country = authorityResult.Country;
                changed = true;
            }

            if (magazine.Language.IsNullOrWhiteSpace() && authorityResult.Language.IsNotNullOrWhiteSpace())
            {
                magazine.Language = authorityResult.Language;
                changed = true;
            }

            return changed;
        }
    }
}
