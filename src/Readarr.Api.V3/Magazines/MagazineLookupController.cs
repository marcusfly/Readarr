using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Metadata;
using Readarr.Http;

namespace Readarr.Api.V3.Magazines
{
    [V1ApiController("magazine/lookup")]
    public class MagazineLookupController : Controller
    {
        private readonly IMagazineService _magazineService;
        private readonly IMagazineTitleAuthorityProvider _titleAuthorityProvider;
        private readonly Logger _logger;

        public MagazineLookupController(IMagazineService magazineService, IMagazineTitleAuthorityProvider titleAuthorityProvider, Logger logger)
        {
            _magazineService = magazineService;
            _titleAuthorityProvider = titleAuthorityProvider;
            _logger = logger;
        }

        public async Task<List<MagazineResource>> Search(string term)
        {
            var searchResults = new List<MagazineResource>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (term.IsNullOrWhiteSpace())
            {
                return searchResults;
            }

            var normalizedSearch = MagazineTitleNormalizer.Normalize(term);
            var localMagazine = _magazineService.FindByNormalizedTitle(normalizedSearch)
                                 ?? _magazineService.FindByNormalizedTitle(term);
            if (localMagazine != null)
            {
                var localResource = localMagazine.ToResource(new List<MagazineIssue>());
                searchResults.Add(localResource);
                seen.Add(localMagazine.Title);
            }

            MagazineAuthorityResult authorityResult;
            try
            {
                authorityResult = await _titleAuthorityProvider.LookupByTitleAsync(term);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to lookup magazine title from authority provider for '{0}'", term);
                return searchResults;
            }

            if (authorityResult == null)
            {
                return searchResults;
            }

            if (authorityResult.CanonicalTitle.IsNotNullOrWhiteSpace())
            {
                var canonical = authorityResult.CanonicalTitle;
                if (!seen.Contains(canonical))
                {
                    var canonicalNormalized = MagazineTitleNormalizer.Normalize(canonical);
                    var knownCanonical = _magazineService.FindByNormalizedTitle(canonicalNormalized);
                    if (knownCanonical != null)
                    {
                        var resource = knownCanonical.ToResource(new List<MagazineIssue>());
                        searchResults.Add(resource);
                    }
                    else
                    {
                        searchResults.Add(new MagazineResource
                        {
                            Title = canonical,
                            CleanTitle = canonical,
                            Issn = authorityResult.Issn,
                            WikidataId = authorityResult.WikidataId,
                            Publisher = authorityResult.Publisher,
                            AddOptions = new AddMagazineOptionsResource { Monitor = MonitorTypes.All, SearchForMissingIssues = false }
                        });
                    }

                    seen.Add(canonical);
                }
            }
            else if (_magazineService.FindByNormalizedTitle(term) == null)
            {
                searchResults.Add(new MagazineResource
                {
                    Title = term,
                    CleanTitle = term,
                    Publisher = authorityResult.Publisher,
                    Issn = authorityResult.Issn,
                    WikidataId = authorityResult.WikidataId,
                    AddOptions = new AddMagazineOptionsResource { Monitor = MonitorTypes.All, SearchForMissingIssues = false }
                });
            }

            return searchResults;
        }
    }
}
