using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.ContentTypes;
using NzbDrone.Core.MediaFiles;

namespace NzbDrone.Core.Books
{
    public static class BookEditionExtensions
    {
        public static List<Edition> GetMonitoredEditions(this Book book)
        {
            return book?.Editions?.Value?.Where(e => e.Monitored).ToList() ?? new List<Edition>();
        }

        public static Edition GetBestMonitoredEdition(this Book book, LibraryContentType contentType = LibraryContentType.None)
        {
            var monitored = book.GetMonitoredEditions();

            if (contentType != LibraryContentType.None)
            {
                return monitored.FirstOrDefault(e => e.GetLibraryContentType() == contentType) ?? monitored.FirstOrDefault();
            }

            return monitored.FirstOrDefault();
        }

        public static Edition GetBestMonitoredEdition(this Book book, BookFile bookFile)
        {
            return book.GetBestMonitoredEdition(bookFile.GetLibraryContentType());
        }
    }
}
