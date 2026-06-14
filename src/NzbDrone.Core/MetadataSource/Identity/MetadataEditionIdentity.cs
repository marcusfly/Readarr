using System;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.Contracts;

namespace NzbDrone.Core.MetadataSource.Identity
{
    public static class MetadataEditionIdentity
    {
        public static string GetMatchKey(Edition edition)
        {
            if (edition?.Isbn13.IsNotNullOrWhiteSpace() == true)
            {
                try
                {
                    return "isbn:" + MetadataIdentifier.NormalizeValue(MetadataEntityType.Isbn, edition.Isbn13);
                }
                catch (ArgumentException)
                {
                    // Provider identity remains stable when external ISBN metadata is malformed.
                }
            }

            return "id:" + edition?.ForeignEditionId;
        }
    }
}
