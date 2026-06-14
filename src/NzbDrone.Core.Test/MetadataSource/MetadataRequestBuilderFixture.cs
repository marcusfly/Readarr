using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Cloud;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class MetadataRequestBuilderFixture : CoreTest<MetadataRequestBuilder>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataSource)
                .Returns("");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataProvider)
                .Returns("openlibrary");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataOpenLibrarySource)
                .Returns("");

            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataRreadingGlassesSource)
                .Returns("");

            Mocker.GetMock<IReadarrCloudRequestBuilder>()
                .Setup(s => s.Metadata)
                .Returns(new HttpRequestBuilder("https://hardcover.bookinfo.pro/{route}").CreateFactory());

            Mocker.GetMock<IReadarrCloudRequestBuilder>()
                .Setup(s => s.MetadataOpenLibrary)
                .Returns(new HttpRequestBuilder("https://openlibrary.org").CreateFactory());

            Mocker.GetMock<IReadarrCloudRequestBuilder>()
                .Setup(s => s.MetadataRreadingGlasses)
                .Returns(new HttpRequestBuilder("https://hardcover.bookinfo.pro/{route}").CreateFactory());
        }

        private void WithCustomProvider(string provider, string url)
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataProvider)
                .Returns(provider);

            if (provider == MetadataRequestBuilder.RreadingGlassesProvider)
            {
                Mocker.GetMock<IConfigService>()
                    .Setup(s => s.MetadataRreadingGlassesSource)
                    .Returns(url);
            }
            else
            {
                Mocker.GetMock<IConfigService>()
                    .Setup(s => s.MetadataOpenLibrarySource)
                    .Returns(url);
            }
        }

        [TestCase]
        public void should_use_user_defined_openlibrary_if_not_blank()
        {
            WithCustomProvider(MetadataRequestBuilder.OpenLibraryProvider, "http://api.readarr.com/api/testing/");

            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("testing");
        }

        [TestCase]
        public void should_use_default_if_config_blank()
        {
            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("openlibrary.org");
        }

        [TestCase]
        public void should_use_rreading_glasses_default_when_selected()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataProvider)
                .Returns(MetadataRequestBuilder.RreadingGlassesProvider);

            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("hardcover.bookinfo.pro");
        }

        [TestCase]
        public void should_default_to_rreading_glasses_when_provider_is_blank()
        {
            Mocker.GetMock<IConfigService>()
                .Setup(s => s.MetadataProvider)
                .Returns(string.Empty);

            var details = Subject.GetRequestBuilder().Create();

            details.BaseUrl.ToString().Should().Contain("hardcover.bookinfo.pro");
        }

        [TestCase]
        public void should_use_provider_specific_builder()
        {
            var details = Subject.GetRequestBuilder(MetadataRequestBuilder.RreadingGlassesProvider).Create();

            details.BaseUrl.ToString().Should().Contain("hardcover.bookinfo.pro");
        }
    }
}
