using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NzbDrone.Core.MetadataSource.Contracts;

namespace NzbDrone.Core.MetadataSource.Identity
{
    public static class DerivedMetadataIdGenerator
    {
        public const string VersionPrefix = "v1_";

        public static MetadataIdentifier Create(string provider, MetadataEntityType entityType, params string[] components)
        {
            return MetadataIdentifier.Create(provider, entityType, CreateVersionedValue(provider, entityType, components));
        }

        public static string CreateVersionedValue(string provider, MetadataEntityType entityType, params string[] components)
        {
            var normalizedProvider = MetadataIdentifier.NormalizeProviderKey(provider);
            var normalizedComponents = NormalizeComponents(components);
            var entityName = MetadataIdentifier.NormalizeEntityName(entityType);
            var payload = string.Join("\u001f", new[] { VersionPrefix.TrimEnd('_'), normalizedProvider, entityName }.Concat(normalizedComponents));

            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(payload);
                var hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder((hash.Length * 2) + VersionPrefix.Length);
                builder.Append(VersionPrefix);

                foreach (var item in hash)
                {
                    builder.Append(item.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static IReadOnlyList<string> NormalizeComponents(IEnumerable<string> components)
        {
            if (components == null)
            {
                throw new ArgumentNullException(nameof(components));
            }

            var normalized = components
                .Select((component, index) =>
                {
                    if (string.IsNullOrWhiteSpace(component))
                    {
                        throw new ArgumentException($"Component {index} is required.", nameof(components));
                    }

                    return component.Trim();
                })
                .ToArray();

            if (normalized.Length == 0)
            {
                throw new ArgumentException("At least one component is required.", nameof(components));
            }

            return normalized;
        }
    }
}
