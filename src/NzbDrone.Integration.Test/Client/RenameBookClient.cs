using System.Collections.Generic;
using Readarr.Api.V3.Books;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class RenameBookClient : ClientBase<RenameBookResource>
    {
        public RenameBookClient(RestClient restClient, string apiKey)
            : base(restClient, apiKey, "rename")
        {
        }

        public List<RenameBookResource> GetPreview(int authorId, int? bookId = null)
        {
            var request = BuildRequest();
            request.AddQueryParameter("authorId", authorId.ToString());

            if (bookId.HasValue)
            {
                request.AddQueryParameter("bookId", bookId.Value.ToString());
            }

            return Get<List<RenameBookResource>>(request);
        }
    }
}
