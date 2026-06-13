using System;
using System.Text.RegularExpressions;
using NzbDrone.Core.MetadataSource.Contracts;

namespace NzbDrone.Core.MetadataSource.Identity
{
    public readonly struct MetadataIdentifier : IEquatable<MetadataIdentifier>
    {
        private static readonly Regex ProviderKeyRegex = new Regex("^[a-z0-9][a-z0-9._-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Isbn10Regex = new Regex("^[0-9]{9}[0-9X]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Isbn13Regex = new Regex("^[0-9]{13}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AsinRegex = new Regex("^[A-Z0-9]{10}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private MetadataIdentifier(string provider, MetadataEntityType entityType, string value)
        {
            Provider = provider;
            EntityType = entityType;
            Value = value;
        }

        public string Provider { get; }

        public MetadataEntityType EntityType { get; }

        public string Value { get; }

        public bool IsEmpty => Provider == null;

        public static MetadataIdentifier Create(string provider, MetadataEntityType entityType, string value)
        {
            var normalizedProvider = NormalizeProviderKey(provider);
            var normalizedValue = NormalizeValue(entityType, value);
            return new MetadataIdentifier(normalizedProvider, entityType, normalizedValue);
        }

        public static MetadataIdentifier Parse(string value)
        {
            if (!TryParse(value, out var identifier))
            {
                throw new FormatException($"'{value}' is not a valid metadata identifier.");
            }

            return identifier;
        }

        public static bool TryParse(string value, out MetadataIdentifier identifier)
        {
            identifier = default;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var firstSeparator = value.IndexOf(':');
            if (firstSeparator <= 0)
            {
                return false;
            }

            var secondSeparator = value.IndexOf(':', firstSeparator + 1);
            if (secondSeparator <= firstSeparator + 1 || secondSeparator == value.Length - 1)
            {
                return false;
            }

            if (value.IndexOf(':', secondSeparator + 1) >= 0)
            {
                return false;
            }

            var providerSegment = value.Substring(0, firstSeparator);
            var entitySegment = value.Substring(firstSeparator + 1, secondSeparator - firstSeparator - 1);
            var valueSegment = value.Substring(secondSeparator + 1);

            if (!TryParseEntityType(entitySegment, out var entityType))
            {
                return false;
            }

            try
            {
                identifier = Create(providerSegment, entityType, valueSegment);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string NormalizeProviderKey(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("Provider key is required.", nameof(provider));
            }

            var normalized = provider.Trim().ToLowerInvariant();
            if (!ProviderKeyRegex.IsMatch(normalized))
            {
                throw new ArgumentException($"'{provider}' is not a valid provider key.", nameof(provider));
            }

            return normalized;
        }

        public static bool TryParseEntityType(string entity, out MetadataEntityType entityType)
        {
            entityType = default;

            if (string.IsNullOrWhiteSpace(entity))
            {
                return false;
            }

            return Enum.TryParse(entity.Trim(), true, out entityType);
        }

        public static string NormalizeEntityName(MetadataEntityType entityType)
        {
            return entityType.ToString().ToLowerInvariant();
        }

        public static string NormalizeValue(MetadataEntityType entityType, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Identifier value is required.", nameof(value));
            }

            var normalized = value.Trim();

            if (normalized.IndexOf(':') >= 0)
            {
                throw new ArgumentException("Identifier value cannot contain ':'.", nameof(value));
            }

            switch (entityType)
            {
                case MetadataEntityType.Isbn:
                    normalized = normalized.Replace("-", string.Empty)
                                           .Replace(" ", string.Empty)
                                           .ToUpperInvariant();

                    if (!Isbn10Regex.IsMatch(normalized) && !Isbn13Regex.IsMatch(normalized))
                    {
                        throw new ArgumentException($"'{value}' is not a valid ISBN.", nameof(value));
                    }

                    break;

                case MetadataEntityType.Asin:
                    normalized = normalized.Replace("-", string.Empty)
                                           .Replace(" ", string.Empty)
                                           .ToUpperInvariant();

                    if (!AsinRegex.IsMatch(normalized))
                    {
                        throw new ArgumentException($"'{value}' is not a valid ASIN.", nameof(value));
                    }

                    break;
            }

            return normalized;
        }

        public bool Equals(MetadataIdentifier other)
        {
            return string.Equals(Provider, other.Provider, StringComparison.Ordinal) &&
                   EntityType == other.EntityType &&
                   string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is MetadataIdentifier other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Provider ?? string.Empty);
                hash = (hash * 31) + (int)EntityType;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
                return hash;
            }
        }

        public override string ToString()
        {
            if (IsEmpty)
            {
                return string.Empty;
            }

            return Provider + ":" + NormalizeEntityName(EntityType) + ":" + Value;
        }

        public static bool operator ==(MetadataIdentifier left, MetadataIdentifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(MetadataIdentifier left, MetadataIdentifier right)
        {
            return !left.Equals(right);
        }
    }
}
