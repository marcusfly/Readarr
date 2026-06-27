using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Metadata
{
    [TestFixture]
    public class IssnPortalEnricherFixture : CoreTest<IssnPortalEnricher>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.EnableIssnPortalLookup)
                .Returns(false);
        }

        [Test]
        public async Task should_return_null_when_disabled()
        {
            (await Subject.LookupByTitleAsync("0951-0281")).Should().BeNull();
        }

        [Test]
        public void should_parse_json_ld_fixture()
        {
            const string payload = @"{
  ""name"": ""Imaginary Weekly"",
  ""issn"": ""0951-0281"",
  ""issnL"": ""0951-0281"",
  ""country"": ""United Kingdom"",
  ""inLanguage"": ""English"",
  ""publisher"": { ""name"": ""Future Publishing"" }
}";

            var result = IssnPortalEnricher.ParseJsonLd(payload);

            result.Should().NotBeNull();
            result.CanonicalTitle.Should().Be("Imaginary Weekly");
            result.NormalizedTitle.Should().Be("imaginary weekly");
            result.Issn.Should().Be("0951-0281");
            result.IssnL.Should().Be("0951-0281");
            result.Country.Should().Be("United Kingdom");
            result.Language.Should().Be("English");
            result.Publisher.Should().Be("Future Publishing");
        }
    }
}
