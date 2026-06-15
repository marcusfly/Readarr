using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Magazines
{
    public interface IMagazineRootFolderService
    {
        List<MagazineRootFolder> GetAll();
        MagazineRootFolder Add(MagazineRootFolder folder);
        void Remove(int id);
        MagazineRootFolder Get(int id);
    }

    public class MagazineRootFolderService : IMagazineRootFolderService
    {
        private readonly IMagazineRootFolderRepository _magazineRootFolderRepository;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;

        public MagazineRootFolderService(IMagazineRootFolderRepository magazineRootFolderRepository,
                                        IDiskProvider diskProvider,
                                        Logger logger)
        {
            _magazineRootFolderRepository = magazineRootFolderRepository;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        public List<MagazineRootFolder> GetAll()
        {
            return _magazineRootFolderRepository.All().ToList();
        }

        public MagazineRootFolder Add(MagazineRootFolder folder)
        {
            VerifyRootFolder(folder);

            if (GetAll().Exists(r => r.Path.PathEquals(folder.Path)))
            {
                throw new InvalidOperationException("Magazine root folder already exists.");
            }

            _magazineRootFolderRepository.Insert(folder);

            GetDetails(folder);

            return folder;
        }

        public void Remove(int id)
        {
            _magazineRootFolderRepository.Delete(id);
        }

        public MagazineRootFolder Get(int id)
        {
            var folder = _magazineRootFolderRepository.Get(id);
            GetDetails(folder);
            return folder;
        }

        private void VerifyRootFolder(MagazineRootFolder rootFolder)
        {
            if (string.IsNullOrWhiteSpace(rootFolder.Path) || !Path.IsPathRooted(rootFolder.Path))
            {
                throw new ArgumentException("Invalid path");
            }

            if (!_diskProvider.FolderExists(rootFolder.Path))
            {
                throw new DirectoryNotFoundException("Can't add root directory that doesn't exist.");
            }

            if (!_diskProvider.FolderWritable(rootFolder.Path))
            {
                throw new UnauthorizedAccessException(string.Format("Root folder path '{0}' is not writable by user '{1}'", rootFolder.Path, Environment.UserName));
            }
        }

        private void GetDetails(MagazineRootFolder folder)
        {
            Task.Run(() =>
            {
                if (_diskProvider.FolderExists(folder.Path))
                {
                    folder.Accessible = true;
                    folder.FreeSpace = _diskProvider.GetAvailableSpace(folder.Path);
                    folder.TotalSpace = _diskProvider.GetTotalSize(folder.Path);
                }
            }).Wait(5000);
        }
    }
}
