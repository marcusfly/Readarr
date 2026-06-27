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
                return EnrichSeedCacheResultAsync(rawTitle, seedCacheResult);
            }

            if (_configService.DisableWikidataLookup)
            {
                _logger.Debug("Skipping magazine title authority lookup for {0} because disable flag is enabled", rawTitle);
                return Task.FromResult<MagazineAuthorityResult>(null);
            }

            return BackfillIssnLAsync(rawTitle);
        }

        private async Task<MagazineAuthorityResult> EnrichSeedCacheResultAsync(string rawTitle, MagazineAuthorityResult seedCacheResult)
        {
            var result = BackfillIssnL(seedCacheResult);

            if (_configService.DisableWikidataLookup || HasArtwork(result))
            {
                return result;
            }

            var wikidataResult = await _wikidataTitleAuthorityImporter.LookupByTitleAsync(result.CanonicalTitle ?? rawTitle);
            if (wikidataResult == null)
            {
                return result;
            }

            return Merge(result, BackfillIssnL(wikidataResult));
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

        private static bool HasArtwork(MagazineAuthorityResult result)
        {
            return result?.ImageUrl.IsNotNullOrWhiteSpace() == true ||
                   result?.LogoUrl.IsNotNullOrWhiteSpace() == true;
        }

        private static MagazineAuthorityResult Merge(MagazineAuthorityResult primary, MagazineAuthorityResult secondary)
        {
            if (primary == null)
            {
                return secondary;
            }

            if (secondary == null)
            {
                return primary;
            }

            primary.WikidataId = primary.WikidataId.IsNotNullOrWhiteSpace() ? primary.WikidataId : secondary.WikidataId;
            primary.Issn = primary.Issn.IsNotNullOrWhiteSpace() ? primary.Issn : secondary.Issn;
            primary.IssnL = primary.IssnL.IsNotNullOrWhiteSpace() ? primary.IssnL : secondary.IssnL;
            primary.Country = primary.Country.IsNotNullOrWhiteSpace() ? primary.Country : secondary.Country;
            primary.Language = primary.Language.IsNotNullOrWhiteSpace() ? primary.Language : secondary.Language;
            primary.Publisher = primary.Publisher.IsNotNullOrWhiteSpace() ? primary.Publisher : secondary.Publisher;
            primary.ImageUrl = primary.ImageUrl.IsNotNullOrWhiteSpace() ? primary.ImageUrl : secondary.ImageUrl;
            primary.LogoUrl = primary.LogoUrl.IsNotNullOrWhiteSpace() ? primary.LogoUrl : secondary.LogoUrl;
            primary.OfficialWebsite = primary.OfficialWebsite.IsNotNullOrWhiteSpace() ? primary.OfficialWebsite : secondary.OfficialWebsite;
            primary.Description = primary.Description.IsNotNullOrWhiteSpace() ? primary.Description : secondary.Description;
            primary.Aliases = primary.Aliases?.Count > 0 ? primary.Aliases : secondary.Aliases;

            return primary;
        }
    }
}
