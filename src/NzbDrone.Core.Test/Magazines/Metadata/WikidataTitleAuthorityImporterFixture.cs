using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Common.TPL;
using NzbDrone.Core.Magazines.Metadata;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Magazines.Metadata
{
    [TestFixture]
    public class WikidataTitleAuthorityImporterFixture : CoreTest<WikidataTitleAuthorityImporter>
    {
        private const string SearchPayload = @"{
  ""search"": [
    {
      ""id"": ""Q99999"",
      ""label"": ""Imaginary Weekly"",
      ""description"": ""Test magazine."",
      ""aliases"": [""Imaginary Weekly"", ""Weekly Imaginary""]
    }
  ]
}";

        private const string DetailsPayload = @"{
  ""results"": {
    ""bindings"": [
      {
        ""issn"": { ""type"": ""literal"", ""value"": ""09510281"" },
        ""issnL"": { ""type"": ""literal"", ""value"": ""09510281"" },
        ""publisherLabel"": { ""type"": ""literal"", ""value"": ""Future Publishing"" },
        ""countryLabel"": { ""type"": ""literal"", ""value"": ""United Kingdom"" },
        ""languageLabel"": { ""type"": ""literal"", ""value"": ""English"" }
      }
    ]
  }
}";

        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IRateLimitService>()
                .Setup(x => x.WaitAndPulseAsync(It.IsAny<string>(), It.IsAny<System.TimeSpan>()))
                .Returns(Task.CompletedTask);

            Mocker.GetMock<IRateLimitService>()
                .Setup(x => x.WaitAndPulseAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.TimeSpan>()))
                .Returns(Task.CompletedTask);
        }

        [Test]
        public async Task should_map_and_normalize_search_and_detail_fields()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request =>
                {
                    if (request.Url.FullUri.Contains("wikidata.org/w/api.php"))
                    {
                        return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, SearchPayload);
                    }

                    request.Url.FullUri.Should().Contain("query.wikidata.org/sparql");
                    return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, DetailsPayload);
                });

            var result = await Subject.LookupByTitleAsync("Imaginary Weekly");

            result.Should().NotBeNull();
            result.CanonicalTitle.Should().Be("Imaginary Weekly");
            result.NormalizedTitle.Should().Be("imaginary weekly");
            result.WikidataId.Should().Be("Q99999");
            result.Issn.Should().Be("0951-0281");
            result.IssnL.Should().Be("0951-0281");
            result.Country.Should().Be("United Kingdom");
            result.Language.Should().Be("English");
            result.Publisher.Should().Be("Future Publishing");
            result.Aliases.Should().Equal("Imaginary Weekly", "Weekly Imaginary");
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Exactly(2));
        }

        [Test]
        public async Task should_return_null_when_lookup_fails()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, @"{ ""search"": [] }"));

            (await Subject.LookupByTitleAsync("Unknown Title")).Should().BeNull();
        }
    }
}
