using Readarr.Api.V3.Author;
using Readarr.Api.V3.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Search
{
    public class SearchResource : RestResource
    {
        public string ForeignId { get; set; }
        public AuthorResource Author { get; set; }
        public BookResource Book { get; set; }
    }
}
