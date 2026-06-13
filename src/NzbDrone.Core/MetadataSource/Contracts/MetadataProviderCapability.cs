using System;

namespace NzbDrone.Core.MetadataSource.Contracts
{
    [Flags]
    public enum MetadataProviderCapability
    {
        None = 0,
        AuthorLookup = 1 << 0,
        BookLookup = 1 << 1,
        AuthorSearch = 1 << 2,
        BookSearch = 1 << 3,
        EntitySearch = 1 << 4,
        IsbnSearch = 1 << 5,
        AsinSearch = 1 << 6,
        ChangeTracking = 1 << 7
    }
}
