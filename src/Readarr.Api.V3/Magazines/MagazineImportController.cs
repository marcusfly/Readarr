using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Services;

namespace Readarr.Api.V3.Magazines
{
    [global::Readarr.Http.V1ApiController("magazineimport")]
    public class MagazineImportController : Controller
    {
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineImportService _magazineImportService;

        public MagazineImportController(IMagazineIssueService magazineIssueService,
                                        IMagazineImportService magazineImportService)
        {
            _magazineIssueService = magazineIssueService;
            _magazineImportService = magazineImportService;
        }

        [HttpGet]
        public List<MagazineImportResource> GetMediaFiles(string path, int? magazineIssueId)
        {
            var issue = magazineIssueId.HasValue && magazineIssueId.Value > 0
                ? _magazineIssueService.GetIssue(magazineIssueId.Value)
                : null;

            return _magazineImportService.GetMediaFiles(path, issue).ToResource();
        }

        [HttpPost]
        public IActionResult UpdateItems([FromBody] List<MagazineImportUpdateResource> resources)
        {
            return Accepted(UpdateImportItems(resources));
        }

        private List<MagazineImportResource> UpdateImportItems(List<MagazineImportUpdateResource> resources)
        {
            resources ??= new List<MagazineImportUpdateResource>();

            var items = resources.Select(resource => new MagazineImportItem
            {
                Path = resource.Path,
                MagazineIssueId = resource.MagazineIssueId,
                Quality = resource.Quality
            }).ToList();

            return _magazineImportService.UpdateItems(items).ToResource();
        }
    }
}
