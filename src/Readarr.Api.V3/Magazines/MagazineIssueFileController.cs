using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Magazines;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    [V1ApiController("magazineissuefile")]
    public class MagazineIssueFileController : RestControllerWithSignalR<MagazineIssueFileResource, NzbDrone.Core.Magazines.MagazineIssueFile>
    {
        private readonly IMagazineIssueFileService _magazineIssueFileService;

        public MagazineIssueFileController(NzbDrone.SignalR.IBroadcastSignalRMessage signalRBroadcaster,
                                          IMagazineIssueFileService magazineIssueFileService)
            : base(signalRBroadcaster)
        {
            _magazineIssueFileService = magazineIssueFileService;
        }

        [HttpGet]
        public List<MagazineIssueFileResource> GetMagazineIssueFiles(int magazineId)
        {
            return _magazineIssueFileService.GetFilesByMagazine(magazineId)
                .Select(x => x.ToResource())
                .ToList();
        }

        [RestDeleteById]
        public void DeleteIssueFile(int id, bool deleteFile = false)
        {
            var issueFile = _magazineIssueFileService.Get(id);
            if (issueFile == null)
            {
                throw new NotFoundException($"Magazine issue file {id} was not found");
            }

            if (deleteFile && !string.IsNullOrWhiteSpace(issueFile.Path) && global::System.IO.File.Exists(issueFile.Path))
            {
                global::System.IO.File.Delete(issueFile.Path);
            }

            _magazineIssueFileService.Delete(id);
        }

        protected override MagazineIssueFileResource GetResourceById(int id)
        {
            var issueFile = _magazineIssueFileService.Get(id);
            return issueFile?.ToResource();
        }
    }
}
