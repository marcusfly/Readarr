using System;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Profiles.Metadata;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.RootFolders
{
    public class DefaultRootFolderService : IHandle<ApplicationStartedEvent>
    {
        private const string DEFAULT_ROOT_FOLDER_PATH = "/books";
        private const string DEFAULT_AUDIOBOOK_ROOT_FOLDER_PATH = "/audiobooks";
        private const string DEFAULT_MAGAZINE_ROOT_FOLDER_PATH = "/magazines";
        private const string ROOT_FOLDER_NAME = "Books";
        private const string AUDIOBOOK_ROOT_FOLDER_NAME = "Audiobooks";
        private const string MAGAZINE_ROOT_FOLDER_NAME = "Magazines";
        private const string ROOT_FOLDER_PATH_ENV_KEY = "READARR__ROOTFOLDER__PATH";
        private const string AUDIOBOOK_ROOT_FOLDER_PATH_ENV_KEY = "READARR__ROOTFOLDER__AUDIOBOOKPATH";
        private const string MAGAZINE_ROOT_FOLDER_PATH_ENV_KEY = "READARR__ROOTFOLDER__MAGAZINEPATH";
        private const int DEFAULT_PROFILE_ID = 1;

        private readonly IRootFolderService _rootFolderService;
        private readonly IMetadataProfileService _metadataProfileService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly Logger _logger;

        public DefaultRootFolderService(IRootFolderService rootFolderService,
                                       IMetadataProfileService metadataProfileService,
                                       IQualityProfileService qualityProfileService,
                                       Logger logger)
        {
            _rootFolderService = rootFolderService;
            _metadataProfileService = metadataProfileService;
            _qualityProfileService = qualityProfileService;
            _logger = logger;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            var existingRootFolders = _rootFolderService.All();

            foreach (var rootFolder in GetDefaultRootFolders())
            {
                if (existingRootFolders.Any(x => x.Path.PathEquals(rootFolder.Path)))
                {
                    continue;
                }

                try
                {
                    _rootFolderService.Add(rootFolder);
                    existingRootFolders.Add(rootFolder);
                    _logger.Info("Created default root folder at '{0}'.", rootFolder.Path);
                }
                catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or ArgumentException)
                {
                    _logger.Warn(ex, "Default root folder could not be created automatically. Path '{0}' is not usable.", rootFolder.Path);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error while creating default root folder '{0}'.", rootFolder.Path);
                }
            }
        }

        private RootFolder[] GetDefaultRootFolders()
        {
            return new[]
            {
                GetDefaultRootFolder(GetConfiguredPath(ROOT_FOLDER_PATH_ENV_KEY, DEFAULT_ROOT_FOLDER_PATH), ROOT_FOLDER_NAME),
                GetDefaultRootFolder(GetConfiguredPath(AUDIOBOOK_ROOT_FOLDER_PATH_ENV_KEY, DEFAULT_AUDIOBOOK_ROOT_FOLDER_PATH), AUDIOBOOK_ROOT_FOLDER_NAME),
                GetDefaultRootFolder(GetConfiguredPath(MAGAZINE_ROOT_FOLDER_PATH_ENV_KEY, DEFAULT_MAGAZINE_ROOT_FOLDER_PATH), MAGAZINE_ROOT_FOLDER_NAME)
            }
            .Where(x => x.Path.IsNotNullOrWhiteSpace())
            .GroupBy(x => x.Path, PathEqualityComparer.Instance)
            .Select(x => x.First())
            .ToArray();
        }

        private RootFolder GetDefaultRootFolder(string rootFolderPath, string fallbackName)
        {
            var rootFolderName = Path.GetFileName(rootFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (rootFolderName.IsNullOrWhiteSpace())
            {
                rootFolderName = fallbackName;
            }

            return new RootFolder
            {
                Name = rootFolderName,
                Path = rootFolderPath,
                DefaultMetadataProfileId = GetDefaultProfileId(_metadataProfileService.All().Select(x => x.Id).ToArray(), "metadata"),
                DefaultQualityProfileId = GetDefaultProfileId(_qualityProfileService.All().Select(x => x.Id).ToArray(), "quality")
            };
        }

        private string GetConfiguredPath(string configKey, string defaultPath)
        {
            return Environment.GetEnvironmentVariable(configKey).IsNotNullOrWhiteSpace()
                ? Environment.GetEnvironmentVariable(configKey)
                : defaultPath;
        }

        private int GetDefaultProfileId(int[] profileIds, string profileType)
        {
            var preferredProfileId = profileIds.OrderBy(id => id).FirstOrDefault();
            if (preferredProfileId > 0)
            {
                return preferredProfileId;
            }

            _logger.Warn("No {0} profiles were available while configuring the default root folder; using fallback profile id '{1}'.", profileType, DEFAULT_PROFILE_ID);
            return DEFAULT_PROFILE_ID;
        }
    }
}
