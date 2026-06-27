using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Magazines;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    [V1ApiController("magazineissue")]
    public class MagazineIssueController : RestControllerWithSignalR<MagazineIssueResource, NzbDrone.Core.Magazines.MagazineIssue>
    {
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineCoverService _magazineCoverService;

        public MagazineIssueController(IMagazineIssueService magazineIssueService,
                                     IMagazineCoverService magazineCoverService,
                                     NzbDrone.SignalR.IBroadcastSignalRMessage signalRBroadcaster)
            : base(signalRBroadcaster)
        {
            _magazineIssueService = magazineIssueService;
            _magazineCoverService = magazineCoverService;
        }

        [HttpGet]
        public List<MagazineIssueResource> GetIssues(int magazineId)
        {
            if (magazineId <= 0)
            {
                throw new NzbDrone.Core.Exceptions.BadRequestException("magazineId is required");
            }

            return _magazineIssueService.GetIssuesByMagazine(magazineId)
                .Select(ToResource)
                .ToList();
        }

        [HttpGet("{id:int}")]
        public ActionResult<MagazineIssueResource> GetIssue(int id)
        {
            return GetResourceByIdWithErrorHandler(id);
        }

        [RestPutById]
        public ActionResult<MagazineIssueResource> UpdateIssue([FromBody] MagazineIssueResource resource)
        {
            var existing = _magazineIssueService.GetIssue(resource.Id);
            var updated = _magazineIssueService.UpsertIssue(ToModel(resource, existing));
            return Accepted(updated.Id);
        }

        [HttpPut("monitor")]
        public ActionResult<List<MagazineIssueResource>> SetMonitored([FromBody] MagazineIssueMonitoredResource resource)
        {
            foreach (var issueId in resource.IssueIds.Distinct())
            {
                _magazineIssueService.MarkIssueMonitored(issueId, resource.Monitored);
            }

            return Accepted(resource.IssueIds
                .Distinct()
                .Select(id => _magazineIssueService.GetIssue(id))
                .Where(x => x != null)
                .Select(ToResource)
                .ToList());
        }

        protected override MagazineIssueResource GetResourceById(int id)
        {
            return ToResource(_magazineIssueService.GetIssue(id));
        }

        private MagazineIssueResource ToResource(MagazineIssue issue)
        {
            var images = _magazineCoverService.GetIssueImages(issue);
            return issue.ToResource(images);
        }

        private MagazineIssue ToModel(MagazineIssueResource resource, MagazineIssue existing)
        {
            if (existing == null)
            {
                var fallback = resource.ToModel();
                return fallback;
            }

            existing.IssueNumber = resource.IssueNumber;
            existing.Monitored = resource.Monitored;
            existing.ReleaseTitle = resource.ReleaseTitle;
            existing.Volume = resource.Volume;
            return existing;
        }
    }
}
