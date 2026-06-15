using System;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Magazines.Metadata;

namespace NzbDrone.Core.Magazines
{
    public interface IAddMagazineService
    {
        Magazine AddMagazine(Magazine magazine);
    }

    public class AddMagazineService : IAddMagazineService
    {
        private readonly IMagazineService _magazineService;
        private readonly IMagazineTitleAuthorityProvider _titleAuthorityProvider;
        private readonly IBuildMagazinePaths _magazinePathBuilder;
        private readonly Logger _logger;

        public AddMagazineService(IMagazineService magazineService,
                                 IMagazineTitleAuthorityProvider titleAuthorityProvider,
                                 IBuildMagazinePaths magazinePathBuilder,
                                 Logger logger)
        {
            _magazineService = magazineService;
            _titleAuthorityProvider = titleAuthorityProvider;
            _magazinePathBuilder = magazinePathBuilder;
            _logger = logger;
        }

        public Magazine AddMagazine(Magazine magazine)
        {
            if (magazine == null)
            {
                throw new ArgumentNullException(nameof(magazine));
            }

            ApplyAuthorityData(magazine);

            magazine.CleanTitle = MagazineTitleNormalizer.Normalize(magazine.Title);
            magazine.NormalizedTitle = magazine.NormalizedTitle.IsNotNullOrWhiteSpace()
                                         ? magazine.NormalizedTitle
                                         : magazine.CleanTitle;

            magazine.Path = magazine.Path.IsNullOrWhiteSpace() ? _magazinePathBuilder.BuildPath(magazine) : magazine.Path;

            if (magazine.AddOptions == null)
            {
                magazine.AddOptions = new AddMagazineOptions();
            }

            var added = _magazineService.AddMagazine(magazine);

            return added;
        }

        private void ApplyAuthorityData(Magazine magazine)
        {
            try
            {
                var result = _titleAuthorityProvider.LookupByTitleAsync(magazine.Title).GetAwaiter().GetResult();
                if (result == null)
                {
                    return;
                }

                magazine.Title = result.CanonicalTitle.IsNotNullOrWhiteSpace() ? result.CanonicalTitle : magazine.Title;
                if (result.NormalizedTitle.IsNotNullOrWhiteSpace())
                {
                    magazine.NormalizedTitle = result.NormalizedTitle;
                }

                magazine.WikidataId = result.WikidataId;
                magazine.Issn = result.Issn;
                magazine.Publisher = result.Publisher;

                if (result.Aliases != null)
                {
                    magazine.Aliases = result.Aliases;
                }
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Magazine title lookup failed for {0}", magazine.Title);
            }
        }
    }
}
