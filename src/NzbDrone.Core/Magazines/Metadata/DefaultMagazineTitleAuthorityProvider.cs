using System.Threading;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Magazines.Metadata
{
    public class DefaultMagazineTitleAuthorityProvider : IMagazineTitleAuthorityProvider
    {
        private readonly IConfigService _configService;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly IIssnLTableImporter _issnLTableImporter;
        private readonly WikidataTitleAuthorityImporter _wikidataTitleAuthorityImporter;
        private readonly Logger _logger;

        public DefaultMagazineTitleAuthorityProvider(IConfigService configService,
                                                     IAppFolderInfo appFolderInfo,
                                                     IDiskProvider diskProvider,
                                                     IIssnLTableImporter issnLTableImporter,
                                                     WikidataTitleAuthorityImporter wikidataTitleAuthorityImporter,
                                                     Logger logger)
        {
            _configService = configService;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _issnLTableImporter = issnLTableImporter;
            _wikidataTitleAuthorityImporter = wikidataTitleAuthorityImporter;
            _logger = logger;
        }

        public Task<MagazineAuthorityResult> LookupByTitleAsync(string rawTitle, CancellationToken ct = default)
        {
            if (rawTitle.IsNullOrWhiteSpace())
            {
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            var manualAliasResult = MagazineManualAliasStore.Lookup(_appFolderInfo, _diskProvider, rawTitle);
            if (manualAliasResult != null)
            {
                _logger.Debug("Magazine title authority lookup matched manual alias for {0}", rawTitle);
                return Task.FromResult(BackfillIssnL(manualAliasResult));
            }

            var seedCacheResult = MagazineSeedCache.Lookup(rawTitle);
            if (seedCacheResult != null)
            {
                _logger.Debug("Magazine title authority lookup matched seed cache for {0}", rawTitle);
                return Task.FromResult(BackfillIssnL(seedCacheResult));
            }

            if (_configService.DisableWikidataLookup)
            {
                _logger.Debug("Skipping magazine title authority lookup for {0} because disable flag is enabled", rawTitle);
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            return BackfillIssnLAsync(rawTitle);
        }

        private async Task<MagazineAuthorityResult> BackfillIssnLAsync(string rawTitle)
        {
            var result = await _wikidataTitleAuthorityImporter.LookupByTitleAsync(rawTitle);
            return BackfillIssnL(result);
        }

        private MagazineAuthorityResult BackfillIssnL(MagazineAuthorityResult result)
        {
            if (result == null)
            {
                return null;
            }

            if (result.Issn.IsNotNullOrWhiteSpace() && result.IssnL.IsNullOrWhiteSpace())
            {
                result.IssnL = _issnLTableImporter?.GetIssnL(result.Issn);
            }

            return result;
        }
    }
}
