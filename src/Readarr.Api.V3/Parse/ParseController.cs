using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Parser;
using Readarr.Api.V3.Author;
using Readarr.Api.V3.Books;
using Readarr.Http;

namespace Readarr.Api.V3.Parse
{
    [V1ApiController]
    public class ParseController : Controller
    {
        private readonly IParsingService _parsingService;

        public ParseController(IParsingService parsingService)
        {
            _parsingService = parsingService;
        }

        [HttpGet]
        public ParseResource Parse(string title)
        {
            if (title.IsNullOrWhiteSpace())
            {
                return null;
            }

            var parsedBookInfo = Parser.ParseBookTitle(title);

            if (parsedBookInfo == null)
            {
                return new ParseResource
                {
                    Title = title
                };
            }

            var remoteBook = _parsingService.Map(parsedBookInfo);

            if (remoteBook != null)
            {
                return new ParseResource
                {
                    Title = title,
                    ParsedBookInfo = remoteBook.ParsedBookInfo,
                    Author = remoteBook.Author.ToResource(),
                    Books = remoteBook.Books.ToResource()
                };
            }
            else
            {
                return new ParseResource
                {
                    Title = title,
                    ParsedBookInfo = parsedBookInfo
                };
            }
        }
    }
}
