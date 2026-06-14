using System.Collections.Generic;
using NzbDrone.Core.History;
using Readarr.Api.V3.History;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class HistoryClient : ClientBase<HistoryResource>
    {
        public HistoryClient(RestClient restClient, string apiKey)
            : base(restClient, apiKey)
        {
        }

        public List<HistoryResource> GetByAuthor(int authorId, int? bookId = null, EntityHistoryEventType? eventType = null, bool includeAuthor = true, bool includeBook = true)
        {
            var request = BuildRequest("author");
            request.AddQueryParameter("authorId", authorId.ToString());
            request.AddQueryParameter("includeAuthor", includeAuthor.ToString().ToLowerInvariant());
            request.AddQueryParameter("includeBook", includeBook.ToString().ToLowerInvariant());

            if (bookId.HasValue)
            {
                request.AddQueryParameter("bookId", bookId.Value.ToString());
            }

            if (eventType.HasValue)
            {
                request.AddQueryParameter("eventType", ((int)eventType.Value).ToString());
            }

            return Get<List<HistoryResource>>(request);
        }
    }
}
