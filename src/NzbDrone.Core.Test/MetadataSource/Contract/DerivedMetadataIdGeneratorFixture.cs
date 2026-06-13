using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;

namespace NzbDrone.Core.Test.MetadataSource.Contract
{
    [TestFixture]
    public class DerivedMetadataIdGeneratorFixture
    {
        [Test]
        public void create_should_return_deterministic_versioned_identifier()
        {
            var identifier = DerivedMetadataIdGenerator.Create("OpenLibrary", MetadataEntityType.Series, "OL123W", "Discworld");

            identifier.Provider.Should().Be("openlibrary");
            identifier.EntityType.Should().Be(MetadataEntityType.Series);
            identifier.Value.Should().Be("v1_9cce655ac681329a70eff7071937e33fcfaa1469b9f119cc59c7eaf032799644");
            identifier.ToString().Should().Be("openlibrary:series:v1_9cce655ac681329a70eff7071937e33fcfaa1469b9f119cc59c7eaf032799644");
        }

        [Test]
        public void create_should_be_stable_for_equivalent_inputs()
        {
            var left = DerivedMetadataIdGenerator.Create("OpenLibrary", MetadataEntityType.Series, "OL123W", "Discworld");
            var right = DerivedMetadataIdGenerator.Create(" openlibrary ", MetadataEntityType.Series, "OL123W", "Discworld");

            left.Should().Be(right);
        }

        [Test]
        public void create_should_change_when_components_change()
        {
            var left = DerivedMetadataIdGenerator.Create("openlibrary", MetadataEntityType.Series, "OL123W", "Discworld");
            var right = DerivedMetadataIdGenerator.Create("openlibrary", MetadataEntityType.Series, "OL123W", "Bromeliad");

            left.Should().NotBe(right);
        }

        [Test]
        public void descriptor_should_expose_contract_version_and_normalized_provider()
        {
            var descriptor = new MetadataProviderDescriptor(" OpenLibrary ", "Open Library", 10, MetadataProviderCapability.AuthorLookup | MetadataProviderCapability.BookLookup);

            descriptor.ProviderKey.Should().Be("openlibrary");
            descriptor.ContractVersion.Should().Be(1);
            descriptor.DisplayName.Should().Be("Open Library");
        }
    }
}
