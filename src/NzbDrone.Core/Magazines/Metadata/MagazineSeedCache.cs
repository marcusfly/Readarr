using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Magazines.Metadata
{
    internal static class MagazineSeedCache
    {
        private static readonly JsonSerializerOptions SerializerSettings = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly Lazy<IReadOnlyDictionary<string, MagazineAuthorityRecord>> Cache = new Lazy<IReadOnlyDictionary<string, MagazineAuthorityRecord>>(Load, true);

        public static MagazineAuthorityResult Lookup(string rawTitle)
        {
            var normalizedTitle = MagazineTitleNormalizer.Normalize(rawTitle);
            if (normalizedTitle.IsNullOrWhiteSpace())
            {
                return null;
            }

            return Cache.Value.TryGetValue(normalizedTitle, out var record)
                ? record.ToResult(rawTitle)
                : null;
        }

        private static IReadOnlyDictionary<string, MagazineAuthorityRecord> Load()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = assembly.GetManifestResourceNames()
                    .SingleOrDefault(x => x.EndsWith("magazines_seed.json", StringComparison.OrdinalIgnoreCase));

                if (resourceName.IsNullOrWhiteSpace())
                {
                    return new Dictionary<string, MagazineAuthorityRecord>(StringComparer.OrdinalIgnoreCase);
                }

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    return new Dictionary<string, MagazineAuthorityRecord>(StringComparer.OrdinalIgnoreCase);
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                var records = JsonSerializer.Deserialize<List<MagazineAuthorityRecord>>(json, SerializerSettings) ?? new List<MagazineAuthorityRecord>();

                var cache = new Dictionary<string, MagazineAuthorityRecord>(StringComparer.OrdinalIgnoreCase);
                foreach (var record in records)
                {
                    Register(cache, record);
                }

                return cache;
            }
            catch
            {
                return new Dictionary<string, MagazineAuthorityRecord>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Register(IDictionary<string, MagazineAuthorityRecord> cache, MagazineAuthorityRecord record)
        {
            if (record == null)
            {
                return;
            }

            AddKey(cache, record.NormalizedTitle, record);
            AddKey(cache, record.RawTitle, record);

            if (record.Aliases == null)
            {
                return;
            }

            foreach (var alias in record.Aliases)
            {
                AddKey(cache, alias, record);
            }
        }

        private static void AddKey(IDictionary<string, MagazineAuthorityRecord> cache, string value, MagazineAuthorityRecord record)
        {
            var key = MagazineTitleNormalizer.Normalize(value);
            if (key.IsNullOrWhiteSpace() || cache.ContainsKey(key))
            {
                return;
            }

            cache[key] = record;
        }
    }
}
