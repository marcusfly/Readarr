using System;
using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;
using NzbDrone.Core.MetadataSource.RreadingGlasses;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.RreadingGlasses
{
    [TestFixture]
    public class RreadingGlassesMetadataProviderFixture : CoreTest<RreadingGlassesMetadataProvider>
    {
        [SetUp]
        public void SetUp()
        {
            var requestBuilderFactory = new HttpRequestBuilder("https://api.bookinfo.club/v1/").CreateFactory();

            Mocker.GetMock<IMetadataRequestBuilder>()
                .Setup(x => x.GetRequestBuilder(MetadataRequestBuilder.RreadingGlassesProvider))
                .Returns(requestBuilderFactory);
        }

        [Test]
        public void should_emit_namespaced_changed_author_ids()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => new HttpResponse(request, new HttpHeader(), @"{ ""authors"": [1, ""2"", { ""key"": ""/authors/3"" }] }", HttpStatusCode.OK));

            var result = Subject.GetChangedAuthors(new DateTime(2026, 6, 12, 0, 0, 0, DateTimeKind.Utc));

            result.Should().BeEquivalentTo(new HashSet<string>
            {
                MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "1").ToString(),
                MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "2").ToString(),
                MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "3").ToString()
            });
        }

        [Test]
        public void should_accept_namespaced_author_ids()
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, useCache, ttl) =>
                {
                    if (request.Url.FullUri.EndsWith("/authors/123.json"))
                    {
                        return new HttpResponse(request, new HttpHeader(), @"{ ""key"": ""/authors/123"", ""name"": ""Example Author"" }", HttpStatusCode.OK);
                    }

                    if (request.Url.FullUri.EndsWith("/authors/123/works.json?limit=50"))
                    {
                        return new HttpResponse(request, new HttpHeader(), @"{ ""entries"": [], ""links"": {} }", HttpStatusCode.OK);
                    }

                    Assert.Fail("Unexpected request: " + request.Url.FullUri);
                    return null;
                });

            var result = Subject.GetAuthorInfo(MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "123").ToString(), false);

            result.ForeignAuthorId.Should().Be(MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, "123").ToString());
        }

        [Test]
        public void should_use_provider_specific_request_builder_for_search()
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, useCache, ttl) =>
                {
                    request.Url.FullUri.Should().StartWith("https://api.bookinfo.club/v1/search/authors.json");
                    return new HttpResponse(request, new HttpHeader(), @"{ ""docs"": [] }", HttpStatusCode.OK);
                });

            Subject.SearchForNewAuthor("discworld").Should().BeEmpty();
        }
    }
}
