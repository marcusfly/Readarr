using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Magazines.Services
{
    public class DeleteMagazineService : IExecute<DeleteMagazineCommand>
    {
        private readonly IMagazineService _magazineService;
        private readonly IDiskProvider _diskProvider;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly Logger _logger;

        public DeleteMagazineService(IMagazineService magazineService,
                                     IDiskProvider diskProvider,
                                     IRecycleBinProvider recycleBinProvider,
                                     Logger logger)
        {
            _magazineService = magazineService;
            _diskProvider = diskProvider;
            _recycleBinProvider = recycleBinProvider;
            _logger = logger;
        }

        public void Execute(DeleteMagazineCommand message)
        {
            if (message == null)
            {
                return;
            }

            var magazine = _magazineService.GetMagazine(message.MagazineId);
            if (magazine == null)
            {
                _logger.Warn("Delete requested for unknown magazine id {0}", message.MagazineId);
                return;
            }

            _logger.Info("Deleting magazine id {0}. Delete files: {1}", message.MagazineId, message.DeleteFiles);
            _magazineService.DeleteMagazine(message.MagazineId, message.DeleteFiles);

            if (!message.DeleteFiles || magazine.Path.IsNullOrWhiteSpace())
            {
                return;
            }

            var otherMagazines = _magazineService.GetAllMagazines()
                .Where(m => m.Id != magazine.Id && m.Path.IsNotNullOrWhiteSpace())
                .ToList();

            if (otherMagazines.Any(m => magazine.Path.PathEquals(m.Path) || magazine.Path.IsParentPath(m.Path)))
            {
                _logger.Warn("Magazine folder '{0}' was retained because it still contains other magazine paths.", magazine.Path);
                return;
            }

            if (!_diskProvider.FolderExists(magazine.Path))
            {
                return;
            }

            _logger.Info("Deleting magazine files from {0}", magazine.Path);
            _recycleBinProvider.DeleteFolder(magazine.Path);
        }
    }
}
