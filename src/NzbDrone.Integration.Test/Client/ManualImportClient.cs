using System.Collections.Generic;
using System.Linq;
using System.Net;
using NzbDrone.Common.Serializer;
using Readarr.Api.V3.ManualImport;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class ManualImportClient : ClientBase<ManualImportResource>
    {
        public ManualImportClient(RestClient restClient, string apiKey)
            : base(restClient, apiKey, "manualimport")
        {
        }

        public List<ManualImportResource> GetMediaFiles(string folder, int? authorId = null, string downloadId = null, bool filterExistingFiles = true, bool replaceExistingFiles = true)
        {
            var request = BuildRequest();
            request.AddQueryParameter("folder", folder);
            request.AddQueryParameter("filterExistingFiles", filterExistingFiles.ToString().ToLowerInvariant());
            request.AddQueryParameter("replaceExistingFiles", replaceExistingFiles.ToString().ToLowerInvariant());

            if (authorId.HasValue)
            {
                request.AddQueryParameter("authorId", authorId.Value.ToString());
            }

            if (downloadId != null)
            {
                request.AddQueryParameter("downloadId", downloadId);
            }

            return Get<List<ManualImportResource>>(request);
        }

        public List<ManualImportResource> UpdateItems(IEnumerable<ManualImportUpdateResource> items)
        {
            var request = BuildRequest();
            request.Method = Method.Post;
            return ExecuteJson<List<ManualImportResource>>(request, items.ToList().ToJson(), HttpStatusCode.Created);
        }
    }
}
