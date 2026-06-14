using System.Collections.Generic;
using Readarr.Api.V3.BookFiles;
using RestSharp;

namespace NzbDrone.Integration.Test.Client
{
    public class BookFileClient : ClientBase<BookFileResource>
    {
        public BookFileClient(RestClient restClient, string apiKey)
            : base(restClient, apiKey, "bookfile")
        {
        }

        public List<BookFileResource> GetByAuthor(int authorId)
        {
            var request = BuildRequest();
            request.AddQueryParameter("authorId", authorId.ToString());

            return Get<List<BookFileResource>>(request);
        }

        public List<BookFileResource> GetByBook(int bookId)
        {
            var request = BuildRequest();
            request.AddQueryParameter("bookId", bookId.ToString());

            return Get<List<BookFileResource>>(request);
        }
    }
}
