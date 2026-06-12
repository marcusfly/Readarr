using System.Collections.Generic;
using NzbDrone.Core.Parser.Model;
using Readarr.Api.V3.Author;
using Readarr.Api.V3.Books;
using Readarr.Http.REST;

namespace Readarr.Api.V3.Parse
{
    public class ParseResource : RestResource
    {
        public string Title { get; set; }
        public ParsedBookInfo ParsedBookInfo { get; set; }
        public AuthorResource Author { get; set; }
        public List<BookResource> Books { get; set; }
    }
}
