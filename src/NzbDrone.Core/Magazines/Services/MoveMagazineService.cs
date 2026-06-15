using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Services
{
    public class MoveMagazineService : IExecute<MoveMagazineCommand>
    {
        private readonly IMagazineService _magazineService;
        private readonly IMagazineIssueFileService _magazineIssueFileService;
        private readonly IDiskProvider _diskProvider;
        private readonly IDiskTransferService _diskTransferService;
        private readonly Logger _logger;

        public MoveMagazineService(IMagazineService magazineService,
                                   IMagazineIssueFileService magazineIssueFileService,
                                   IDiskProvider diskProvider,
                                   IDiskTransferService diskTransferService,
                                   Logger logger)
        {
            _magazineService = magazineService;
            _magazineIssueFileService = magazineIssueFileService;
            _diskProvider = diskProvider;
            _diskTransferService = diskTransferService;
            _logger = logger;
        }

        public void Execute(MoveMagazineCommand message)
        {
            if (message == null)
            {
                return;
            }

            var magazine = _magazineService.GetMagazine(message.MagazineId);
            if (magazine == null)
            {
                _logger.Warn("Magazine move requested for unknown magazine id {0}", message.MagazineId);
                return;
            }

            var sourcePath = message.SourcePath ?? magazine.Path;
            var destinationPath = message.DestinationPath;

            if (sourcePath.IsNullOrWhiteSpace() || destinationPath.IsNullOrWhiteSpace())
            {
                _logger.Warn("Magazine move requested for '{0}' without both source and destination paths.", magazine.Title);
                return;
            }

            if (!_diskProvider.FolderExists(sourcePath))
            {
                _logger.Debug("Folder '{0}' for '{1}' does not exist, not moving.", sourcePath, magazine.Title);
                return;
            }

            if (sourcePath.PathEquals(destinationPath))
            {
                _logger.ProgressInfo("{0} is already in the specified location '{1}'.", magazine, destinationPath);
                return;
            }

            _logger.ProgressInfo("Moving {0} from '{1}' to '{2}'", magazine.Title, sourcePath, destinationPath);

            try
            {
                _diskTransferService.TransferFolder(sourcePath, destinationPath, TransferMode.Move);

                MoveIssueFiles(magazine.Id, sourcePath, destinationPath);

                magazine.Path = destinationPath;
                _magazineService.UpdateMagazine(magazine);

                _logger.ProgressInfo("{0} moved successfully to {1}", magazine.Title, destinationPath);
            }
            catch (IOException ex)
            {
                _logger.Error(ex, "Unable to move magazine from '{0}' to '{1}'. Try moving files manually", sourcePath, destinationPath);
            }
        }

        private void MoveIssueFiles(int magazineId, string sourcePath, string destinationPath)
        {
            var issueFiles = _magazineIssueFileService.GetFilesByMagazine(magazineId);

            foreach (var issueFile in issueFiles.Where(file => sourcePath.IsParentPath(file.Path) || sourcePath.PathEquals(file.Path)))
            {
                var relativePath = sourcePath.PathEquals(issueFile.Path)
                    ? string.Empty
                    : sourcePath.GetRelativePath(issueFile.Path);

                issueFile.Path = string.IsNullOrWhiteSpace(relativePath)
                    ? destinationPath
                    : Path.Combine(destinationPath, relativePath);

                _magazineIssueFileService.Update(issueFile);
            }
        }
    }
}
