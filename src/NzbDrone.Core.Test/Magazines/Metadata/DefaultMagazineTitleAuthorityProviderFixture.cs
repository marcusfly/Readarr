using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Metadata
{
    [TestFixture]
    public class DefaultMagazineTitleAuthorityProviderFixture : CoreTest<DefaultMagazineTitleAuthorityProvider>
    {
        private const string WikidataPayload = @"{
  ""search"": [
    {
      ""id"": ""Q12345"",
      ""label"": ""Wired Magazine"",
      ""description"": ""American technology magazine."",
      ""aliases"": [""Wired"", ""WIRED""]
    }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.DisableWikidataLookup)
                .Returns(false);
        }

        [Test]
        public async Task should_skip_lookup_when_wikidata_is_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.DisableWikidataLookup)
                .Returns(true);

            var result = await Subject.LookupByTitleAsync("Wired Magazine");

            result.Should().BeNull();
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_return_authority_match_from_wikidata()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request =>
                {
                    request.Url.FullUri.Should().Contain("wikidata.org/w/api.php");
                    request.Url.FullUri.Should().Contain("action=wbsearchentities");
                    request.Url.FullUri.Should().Contain("search=Wired");
                    request.SuppressHttpError.Should().BeTrue();

                    return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, WikidataPayload);
                });

            var result = await Subject.LookupByTitleAsync("  Wired Magazine  ");

            result.Should().NotBeNull();
            result.CanonicalTitle.Should().Be("Wired Magazine");
            result.NormalizedTitle.Should().Be("wired magazine");
            result.WikidataId.Should().Be("Q12345");
            result.Aliases.Should().Equal("Wired", "WIRED");
            result.Publisher.Should().Be("American technology magazine.");
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Once());
        }
    }
}
