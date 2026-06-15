using System.Collections.Generic;
using Readarr.Api.V3.Books;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class BookLookupClient : ClientBase<BookResource>
    {
        public BookLookupClient(RestClient restClient, string apiKey)
            : base(restClient, apiKey, "book/lookup")
        {
        }

        public List<BookResource> Lookup(string term)
        {
            var request = BuildRequest();
            request.AddQueryParameter("term", term);

            return Get<List<BookResource>>(request);
        }
    }
}
