using System;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.ContentTypes
{
    public static class LibraryContentTypeExtensions
    {
        private static readonly string[] EbookFormats =
        {
            "Kindle Edition",
            "Nook",
            "ebook"
        };

        private static readonly string[] AudiobookFormats =
        {
            "Audiobook",
            "Audio CD",
            "Audio Cassette",
            "Audible Audio",
            "CD-ROM",
            "MP3 CD"
        };

        public static LibraryContentType ToLibraryContentType(this Quality quality)
        {
            if (quality == null)
            {
                return LibraryContentType.None;
            }

            if (quality == Quality.MP3 ||
                quality == Quality.M4B ||
                quality == Quality.FLAC ||
                quality == Quality.UnknownAudio)
            {
                return LibraryContentType.Audiobook;
            }

            return LibraryContentType.Book;
        }

        public static LibraryContentType GetAllowedContentTypes(this QualityProfile profile)
        {
            if (profile?.Items == null)
            {
                return LibraryContentType.None;
            }

            return profile.Items
                          .Where(i => i.Allowed)
                          .SelectMany(i => i.GetQualities())
                          .Aggregate(LibraryContentType.None, (current, quality) => current | quality.ToLibraryContentType());
        }

        public static LibraryContentType GetLibraryContentType(this Edition edition)
        {
            if (edition == null)
            {
                return LibraryContentType.None;
            }

            if (edition.IsEbook || IsEbookFormat(edition.Format))
            {
                return LibraryContentType.Book;
            }

            if (IsAudiobookFormat(edition.Format))
            {
                return LibraryContentType.Audiobook;
            }

            return LibraryContentType.Book;
        }

        public static LibraryContentType GetLibraryContentType(this BookFile bookFile)
        {
            if (bookFile == null)
            {
                return LibraryContentType.None;
            }

            var extensionType = MediaFileExtensions.GetContentTypeForExtension(bookFile.Path?.GetPathExtension());
            if (extensionType != LibraryContentType.None)
            {
                return extensionType;
            }

            return bookFile.Quality?.Quality.ToLibraryContentType() ?? LibraryContentType.None;
        }

        public static LibraryContentType GetLibraryContentType(this QualityModel quality)
        {
            return quality?.Quality.ToLibraryContentType() ?? LibraryContentType.None;
        }

        public static LibraryContentType GetLibraryContentType(this LocalBook localBook)
        {
            if (localBook == null)
            {
                return LibraryContentType.None;
            }

            var extensionType = MediaFileExtensions.GetContentTypeForExtension(localBook.Path?.GetPathExtension());
            if (extensionType != LibraryContentType.None)
            {
                return extensionType;
            }

            return localBook.Quality?.Quality.ToLibraryContentType() ?? LibraryContentType.None;
        }

        public static LibraryContentType GetLibraryContentType(this RemoteBook remoteBook)
        {
            return remoteBook?.ParsedBookInfo?.Quality.GetLibraryContentType() ?? LibraryContentType.None;
        }

        public static bool HasContentType(this LibraryContentType source, LibraryContentType target)
        {
            return (source & target) == target;
        }

        private static bool IsEbookFormat(string format)
        {
            return format.IsNotNullOrWhiteSpace() &&
                   EbookFormats.Any(f => f.Equals(format, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAudiobookFormat(string format)
        {
            return format.IsNotNullOrWhiteSpace() &&
                   AudiobookFormats.Any(f => f.Equals(format, StringComparison.OrdinalIgnoreCase));
        }
    }
}
