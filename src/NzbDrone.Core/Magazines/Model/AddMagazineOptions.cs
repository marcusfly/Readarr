using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Magazines
{
    public class AddMagazineOptions : IEmbeddedDocument
    {
        public MonitorTypes Monitor { get; set; }
        public bool SearchForMissingIssues { get; set; }
    }
}
