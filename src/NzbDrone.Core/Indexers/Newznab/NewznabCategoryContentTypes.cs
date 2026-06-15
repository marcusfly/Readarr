using System;
using NzbDrone.Core.ContentTypes;

namespace NzbDrone.Core.Indexers.Newznab
{
    public static class NewznabCategoryContentTypes
    {
        public static LibraryContentType GetContentTypes(NewznabCategory category)
        {
            if (category == null)
            {
                return LibraryContentType.None;
            }

            if (category.ContentTypes != LibraryContentType.None)
            {
                return category.ContentTypes;
            }

            return GetContentTypes(category.Id, category.Name);
        }

        public static LibraryContentType GetContentTypes(int categoryId, string categoryName = null)
        {
            switch (categoryId)
            {
                case 3000:
                case 3030:
                    return LibraryContentType.Audiobook;
                case 7000:
                    return LibraryContentType.Book | LibraryContentType.Comic | LibraryContentType.Magazine;
                case 7010:
                case 7020:
                    return LibraryContentType.Book;
                case 7030:
                    return LibraryContentType.Comic;
                case 7040:
                    return LibraryContentType.Magazine;
                default:
                    return GetContentTypesFromName(categoryName);
            }
        }

        private static LibraryContentType GetContentTypesFromName(string categoryName)
        {
            if (categoryName == null)
            {
                return LibraryContentType.None;
            }

            if (categoryName.Contains("audio", StringComparison.OrdinalIgnoreCase))
            {
                return LibraryContentType.Audiobook;
            }

            if (categoryName.Contains("comic", StringComparison.OrdinalIgnoreCase))
            {
                return LibraryContentType.Comic;
            }

            if (categoryName.Contains("magazine", StringComparison.OrdinalIgnoreCase))
            {
                return LibraryContentType.Magazine;
            }

            if (categoryName.Contains("book", StringComparison.OrdinalIgnoreCase) ||
                categoryName.Contains("ebook", StringComparison.OrdinalIgnoreCase))
            {
                return LibraryContentType.Book;
            }

            return LibraryContentType.None;
        }
    }
}
