using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Jobs.Durable;
using Readarr.Http;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Jobs
{
    [V1ApiController("jobs")]
    public class JobAttemptController : RestController<JobAttemptResource>
    {
        private readonly IJobAttemptService _jobAttemptService;

        public JobAttemptController(IJobAttemptService jobAttemptService)
        {
            _jobAttemptService = jobAttemptService;
        }

        protected override JobAttemptResource GetResourceById(int id)
        {
            return _jobAttemptService.GetById(id).ToResource();
        }

        [HttpGet]
        public List<JobAttemptResource> GetAll()
        {
            return _jobAttemptService.GetAll()
                .Select(x => x.ToResource())
                .ToList();
        }
    }
}
