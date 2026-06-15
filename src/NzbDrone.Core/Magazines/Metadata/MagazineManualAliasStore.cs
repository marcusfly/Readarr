using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Magazines.Metadata
{
    internal static class MagazineManualAliasStore
    {
        private const string ManualAliasFileName = "magazine_aliases.json";

        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static MagazineAuthorityResult Lookup(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider, string rawTitle)
        {
            var normalizedTitle = MagazineTitleNormalizer.Normalize(rawTitle);
            if (normalizedTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            try
            {
                var aliasPath = Path.Combine(appFolderInfo.GetAppDataPath(), ManualAliasFileName);
                if (!diskProvider.FileExists(aliasPath))
                {
                    return null;
                }

                var json = diskProvider.ReadAllText(aliasPath);
                if (json.IsNullOrWhiteSpace())
                {
                    return null;
                }

                var records = JsonSerializer.Deserialize<List<MagazineAuthorityRecord>>(json, SerializerSettings) ?? new List<MagazineAuthorityRecord>();
                var record = records.FirstOrDefault(x => Matches(x, normalizedTitle));

                return record?.ToResult(rawTitle);
            }
            catch
            {
                return null;
            }
        }

        private static bool Matches(MagazineAuthorityRecord record, string normalizedTitle)
        {
            if (record == null)
            {
                return false;
            }

            var normalizedCanonical = MagazineTitleNormalizer.Normalize(record.NormalizedTitle ?? record.CanonicalTitle ?? record.RawTitle);
            if (normalizedCanonical.Equals(normalizedTitle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (record.Aliases == null)
            {
                return false;
            }

            return record.Aliases.Any(alias => MagazineTitleNormalizer.Normalize(alias).Equals(normalizedTitle, StringComparison.OrdinalIgnoreCase));
        }
    }
}
