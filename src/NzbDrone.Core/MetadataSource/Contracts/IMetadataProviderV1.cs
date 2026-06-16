using System;
using System.Collections.Generic;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.Identity;

namespace NzbDrone.Core.MetadataSource.Contracts
{
    public interface IMetadataProviderV1
    {
        MetadataProviderDescriptor Descriptor { get; }

        bool SupportsIdentifier(MetadataIdentifier identifier);

        Author GetAuthorInfo(string readarrId, bool useCache = true);

        Tuple<string, Book, List<AuthorMetadata>> GetBookInfo(string id);

        List<Author> SearchForNewAuthor(string title);

        List<Book> SearchForNewBook(string title, string author, bool getAllEditions = true);

        List<Book> SearchByIsbn(string isbn);

        List<Book> SearchByAsin(string asin);

        List<object> SearchForNewEntity(string title, NewItemSearchScope scope = NewItemSearchScope.All);

        HashSet<string> GetChangedAuthors(DateTime startTime);
    }
}
