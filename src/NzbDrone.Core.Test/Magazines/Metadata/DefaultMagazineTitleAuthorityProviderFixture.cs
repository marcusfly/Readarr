using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
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
      ""id"": ""Q99999"",
      ""label"": ""Imaginary Weekly"",
      ""description"": ""Test magazine."",
      ""aliases"": [""Imaginary Weekly"", ""Weekly Imaginary""]
    }
  ]
}";

        private string _appDataFolder;

        [SetUp]
        public void SetUp()
        {
            _appDataFolder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "magazine-appdata");
            Directory.CreateDirectory(_appDataFolder);

            Mocker.GetMock<IAppFolderInfo>()
                .SetupGet(x => x.AppDataFolder)
                .Returns(_appDataFolder);

            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.DisableWikidataLookup)
                .Returns(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_appDataFolder))
            {
                Directory.Delete(_appDataFolder, true);
            }
        }

        [Test]
        public async Task should_return_manual_alias_before_seed_cache_and_wikidata()
        {
            var aliasPath = Path.Combine(_appDataFolder, "magazine_aliases.json");
            File.WriteAllText(aliasPath, @"[
  {
    ""canonicalTitle"": ""Wired Magazine"",
    ""normalizedTitle"": ""wired magazine"",
    ""aliases"": [""Wired"", ""WIRED""],
    ""publisher"": ""Manual Override""
  }
]");

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.FileExists(aliasPath))
                .Returns(true);

            Mocker.GetMock<IDiskProvider>()
                .Setup(x => x.ReadAllText(aliasPath))
                .Returns(File.ReadAllText(aliasPath));

            var result = await Subject.LookupByTitleAsync("Wired");

            result.Should().NotBeNull();
            result.CanonicalTitle.Should().Be("Wired Magazine");
            result.NormalizedTitle.Should().Be("wired magazine");
            result.Publisher.Should().Be("Manual Override");
            result.Aliases.Should().Contain("Wired");
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_return_seed_cache_match_before_wikidata()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.DisableWikidataLookup)
                .Returns(true);

            var result = await Subject.LookupByTitleAsync("Motor Trend");

            result.Should().NotBeNull();
            result.CanonicalTitle.Should().Be("MotorTrend");
            result.NormalizedTitle.Should().Be("motortrend");
            result.Aliases.Should().Contain("motor trend");
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Never());
        }

        [Test]
        public async Task should_skip_lookup_when_wikidata_is_disabled()
        {
            Mocker.GetMock<IConfigService>()
                .SetupGet(x => x.DisableWikidataLookup)
                .Returns(true);

            var result = await Subject.LookupByTitleAsync("NoSuchMagazine");

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
                    request.Url.FullUri.Should().Contain("search=ImaginaryWeekly");
                    request.SuppressHttpError.Should().BeTrue();

                    return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, WikidataPayload);
                });

            var result = await Subject.LookupByTitleAsync("  ImaginaryWeekly  ");

            result.Should().NotBeNull();
            result.CanonicalTitle.Should().Be("Imaginary Weekly");
            result.NormalizedTitle.Should().Be("imaginary weekly");
            result.WikidataId.Should().Be("Q99999");
            result.Aliases.Should().Equal("Imaginary Weekly", "Weekly Imaginary");
            result.Publisher.Should().Be("Test magazine.");
            Mocker.GetMock<IHttpClient>().Verify(x => x.Get(It.IsAny<HttpRequest>()), Times.Once());
        }
    }
}
