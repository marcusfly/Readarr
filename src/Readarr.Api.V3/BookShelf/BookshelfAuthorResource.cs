using System.Collections.Generic;
using Readarr.Api.V3.Books;

namespace Readarr.Api.V3.Bookshelf
{
    public class BookshelfAuthorResource
    {
        public int Id { get; set; }
        public bool? Monitored { get; set; }
        public List<BookResource> Books { get; set; }
    }
}
