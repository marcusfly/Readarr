using System.Collections.Generic;

namespace Readarr.Api.V3.Author
{
    public class AuthorEditorDeleteResource
    {
        public List<int> AuthorIds { get; set; }
        public bool DeleteFiles { get; set; }
    }
}
