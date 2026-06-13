using System.Collections.Generic;
using NzbDrone.Core.Qualities;

namespace Readarr.Api.V3.BookFiles
{
    public class BookFileListResource
    {
        public List<int> BookFileIds { get; set; }
        public QualityModel Quality { get; set; }
    }
}
