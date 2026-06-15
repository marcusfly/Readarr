using System;

namespace NzbDrone.Core.ContentTypes
{
    [Flags]
    public enum LibraryContentType
    {
        None = 0,
        Book = 1 << 0,
        Audiobook = 1 << 1,
        Comic = 1 << 2,
        Magazine = 1 << 3
    }
}
