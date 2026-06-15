using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Magazines;
using NzbDrone.Core.Magazines.Commands;
using NzbDrone.Core.Magazines.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Http.REST.Attributes;
using NzbDrone.SignalR;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Magazines
{
    [V1ApiController("magazine")]
    public class MagazineController : RestControllerWithSignalR<MagazineResource, NzbDrone.Core.Magazines.Magazine>,
                                     IHandle<MagazineAddedEvent>,
                                     IHandle<MagazineUpdatedEvent>,
                                     IHandle<MagazineDeletedEvent>
    {
        private readonly IAddMagazineService _addMagazineService;
        private readonly IMagazineService _magazineService;
        private readonly IMagazineIssueService _magazineIssueService;
        private readonly IMagazineMonitoredService _magazineMonitoredService;
        private readonly IManageCommandQueue _commandQueueManager;

        public MagazineController(IBroadcastSignalRMessage signalRBroadcaster,
                                 IAddMagazineService addMagazineService,
                                 IMagazineService magazineService,
                                 IMagazineIssueService magazineIssueService,
                                 IMagazineMonitoredService magazineMonitoredService,
                                 IManageCommandQueue commandQueueManager)
            : base(signalRBroadcaster)
        {
            _addMagazineService = addMagazineService;
            _magazineService = magazineService;
            _magazineIssueService = magazineIssueService;
            _magazineMonitoredService = magazineMonitoredService;
            _commandQueueManager = commandQueueManager;
        }

        [HttpGet]
        public List<MagazineResource> GetAll()
        {
            return _magazineService.GetAllMagazines()
                .Select(x => x.ToResource(_magazineIssueService.GetIssuesByMagazine(x.Id)))
                .ToList();
        }

        [RestPostById]
        public ActionResult<MagazineResource> Add([FromBody] MagazineResource resource)
        {
            var added = _addMagazineService.AddMagazine(resource.ToModel());

            if (added.AddOptions != null)
            {
                _magazineMonitoredService.SetIssueMonitoredStatus(added, added.AddOptions.Monitor);
            }

            return Created(added.Id);
        }

        [RestPutById]
        public ActionResult<MagazineResource> Update([FromBody] MagazineResource resource)
        {
            var existing = _magazineService.GetMagazine(resource.Id);
            var updated = _magazineService.UpdateMagazine(resource.ToModel(existing));

            if (updated.AddOptions != null)
            {
                _magazineMonitoredService.SetIssueMonitoredStatus(updated, updated.AddOptions.Monitor);
            }

            return Accepted(updated.Id);
        }

        [RestDeleteById]
        public void DeleteMagazine(int id, bool deleteFiles = false)
        {
            _commandQueueManager.Push(new DeleteMagazineCommand
            {
                MagazineId = id,
                DeleteFiles = deleteFiles
            });
        }

        [NonAction]
        public void Handle(MagazineAddedEvent message)
        {
            BroadcastResourceChange(ModelAction.Created, message.Magazine.Id);
        }

        [NonAction]
        public void Handle(MagazineUpdatedEvent message)
        {
            BroadcastResourceChange(ModelAction.Updated, message.Magazine.Id);
        }

        [NonAction]
        public void Handle(MagazineDeletedEvent message)
        {
            BroadcastResourceChange(ModelAction.Deleted, message.Magazine.Id);
        }

        protected override MagazineResource GetResourceById(int id)
        {
            var magazine = _magazineService.GetMagazine(id);
            var issues = _magazineIssueService.GetIssuesByMagazine(id);
            return magazine.ToResource(issues);
        }

        protected override MagazineResource GetResourceByIdForBroadcast(int id)
        {
            return GetResourceById(id);
        }
    }
}
