using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Magazines;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    [V1ApiController("magazinerootfolder")]
    public class MagazineRootFolderController : RestControllerWithSignalR<MagazineRootFolderResource, NzbDrone.Core.Magazines.MagazineRootFolder>
    {
        private readonly IMagazineRootFolderService _magazineRootFolderService;

        public MagazineRootFolderController(IBroadcastSignalRMessage signalRBroadcaster,
                                           IMagazineRootFolderService magazineRootFolderService)
            : base(signalRBroadcaster)
        {
            _magazineRootFolderService = magazineRootFolderService;
        }

        [HttpGet]
        public List<MagazineRootFolderResource> GetAll()
        {
            return _magazineRootFolderService.GetAll()
                .Select(x => x.ToResource())
                .ToList();
        }

        [RestPostById]
        public ActionResult<MagazineRootFolderResource> Create([FromBody] MagazineRootFolderResource resource)
        {
            var folder = _magazineRootFolderService.Add(resource.ToModel());
            return Created(folder.Id);
        }

        [RestDeleteById]
        public void DeleteFolder(int id)
        {
            _magazineRootFolderService.Remove(id);
        }

        protected override MagazineRootFolderResource GetResourceById(int id)
        {
            return _magazineRootFolderService.Get(id).ToResource();
        }
    }
}
