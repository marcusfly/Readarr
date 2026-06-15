using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using NLog;
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
        private const string ROOT_FOLDER_NAME = "Books";
        private const string ROOT_FOLDER_PATH_CONFIG_KEY = "Readarr:RootFolder:Path";
        private const int DEFAULT_PROFILE_ID = 1;

        private readonly IRootFolderService _rootFolderService;
        private readonly IMetadataProfileService _metadataProfileService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly IConfiguration _configuration;
        private readonly Logger _logger;

        public DefaultRootFolderService(IRootFolderService rootFolderService,
                                       IMetadataProfileService metadataProfileService,
                                       IQualityProfileService qualityProfileService,
                                       IConfiguration configuration,
                                       Logger logger)
        {
            _rootFolderService = rootFolderService;
            _metadataProfileService = metadataProfileService;
            _qualityProfileService = qualityProfileService;
            _configuration = configuration;
            _logger = logger;
        }

        public void Handle(ApplicationStartedEvent message)
        {
            if (_rootFolderService.All().Any())
            {
                return;
            }

            try
            {
                _rootFolderService.Add(GetDefaultRootFolder());
                _logger.Info("Created default root folder at '{0}'.", GetRootFolderPath());
            }
            catch (Exception ex) when (ex is DirectoryNotFoundException or UnauthorizedAccessException or ArgumentException)
            {
                _logger.Warn(ex, "Default root folder could not be created automatically. Path '{0}' is not usable.", GetRootFolderPath());
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error while creating default root folder.");
            }
        }

        private RootFolder GetDefaultRootFolder()
        {
            var rootFolderPath = GetRootFolderPath();
            var rootFolderName = Path.GetFileName(rootFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (rootFolderName.IsNullOrWhiteSpace())
            {
                rootFolderName = ROOT_FOLDER_NAME;
            }

            return new RootFolder
            {
                Name = rootFolderName,
                Path = rootFolderPath,
                DefaultMetadataProfileId = GetDefaultProfileId(_metadataProfileService.All().Select(x => x.Id).ToArray(), "metadata"),
                DefaultQualityProfileId = GetDefaultProfileId(_qualityProfileService.All().Select(x => x.Id).ToArray(), "quality")
            };
        }

        private string GetRootFolderPath()
        {
            return _configuration[ROOT_FOLDER_PATH_CONFIG_KEY].IsNotNullOrWhiteSpace()
                ? _configuration[ROOT_FOLDER_PATH_CONFIG_KEY]
                : DEFAULT_ROOT_FOLDER_PATH;
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
