using System;
using NzbDrone.Core.MetadataSource.Identity;

namespace NzbDrone.Core.MetadataSource.Contracts
{
    public sealed class MetadataProviderDescriptor : IEquatable<MetadataProviderDescriptor>
    {
        public const int CurrentContractVersion = 1;

        public MetadataProviderDescriptor(string providerKey, string displayName, int priority, MetadataProviderCapability capabilities)
        {
            ProviderKey = MetadataIdentifier.NormalizeProviderKey(providerKey);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? throw new ArgumentException("Display name is required.", nameof(displayName)) : displayName.Trim();
            Priority = priority;
            Capabilities = capabilities;
        }

        public string ProviderKey { get; }

        public int ContractVersion => CurrentContractVersion;

        public string DisplayName { get; }

        public int Priority { get; }

        public MetadataProviderCapability Capabilities { get; }

        public bool Equals(MetadataProviderDescriptor other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return string.Equals(ProviderKey, other.ProviderKey, StringComparison.Ordinal) &&
                   ContractVersion == other.ContractVersion &&
                   string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal) &&
                   Priority == other.Priority &&
                   Capabilities == other.Capabilities;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as MetadataProviderDescriptor);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(ProviderKey);
                hash = (hash * 31) + ContractVersion;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(DisplayName);
                hash = (hash * 31) + Priority;
                hash = (hash * 31) + (int)Capabilities;
                return hash;
            }
        }
    }
}
