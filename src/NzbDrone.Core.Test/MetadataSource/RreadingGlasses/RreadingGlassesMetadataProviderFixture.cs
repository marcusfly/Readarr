using System;
using System.Linq;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.RreadingGlasses;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.RreadingGlasses
{
    [TestFixture]
    public class RreadingGlassesMetadataProviderFixture : CoreTest<RreadingGlassesMetadataProvider>
    {
        private const string AuthorPayload = @"{
  ""ForeignId"": 123,
  ""Name"": ""Example Author"",
  ""Description"": ""Author biography."",
  ""ImageUrl"": ""https://images.example/author.jpg"",
  ""Url"": ""https://source.example/author/123"",
  ""RatingCount"": 50,
  ""AverageRating"": 4.2,
  ""Works"": [
    {
      ""ForeignId"": 456,
      ""Title"": ""Example Book"",
      ""FullTitle"": ""Example Book: A Novel"",
      ""Url"": ""https://source.example/work/456"",
      ""Genres"": [""Fantasy""],
      ""Authors"": [{ ""ForeignId"": 123, ""Name"": ""Example Author"" }],
      ""Books"": [
        {
          ""ForeignId"": 789,
          ""Title"": ""Example Book"",
          ""Isbn13"": ""978-0-439-55493-0"",
          ""Asin"": ""b00jcdk5me"",
          ""RatingCount"": 20,
          ""AverageRating"": 4.5,
          ""Url"": ""https://source.example/book/789"",
          ""Contributors"": [{ ""ForeignId"": 123, ""Role"": ""Author"" }]
        }
      ],
      ""Series"": [
        {
          ""ForeignId"": 321,
          ""Title"": ""Example Series"",
          ""LinkItems"": [
            {
              ""ForeignWorkId"": 456,
              ""PositionInSeries"": ""1"",
              ""SeriesPosition"": 1,
              ""Primary"": true
            }
          ]
        }
      ]
    }
  ],
  ""Series"": [
    {
      ""ForeignId"": 321,
      ""Title"": ""Example Series"",
      ""LinkItems"": [
        {
          ""ForeignWorkId"": 456,
          ""PositionInSeries"": ""1"",
          ""SeriesPosition"": 1,
          ""Primary"": true
        }
      ]
    }
  ]
}";

        [SetUp]
        public void SetUp()
        {
            var requestBuilderFactory = new HttpRequestBuilder("https://hardcover.bookinfo.pro").CreateFactory();

            Mocker.GetMock<IMetadataRequestBuilder>()
                .Setup(x => x.GetRequestBuilder(MetadataRequestBuilder.RreadingGlassesProvider))
                .Returns(requestBuilderFactory);
        }

        [Test]
        public void should_use_real_author_endpoint_and_emit_namespaced_ids()
        {
            MockCachedResponse("author/123", AuthorPayload);

            var author = Subject.GetAuthorInfo("rreading-glasses:author:123", false);

            author.ForeignAuthorId.Should().Be("rreading-glasses:author:123");
            author.Books.Value.Should().ContainSingle();
            author.Books.Value[0].ForeignBookId.Should().Be("rreading-glasses:work:456");
            author.Books.Value[0].Editions.Value[0].ForeignEditionId.Should().Be("rreading-glasses:edition:789");
            author.Books.Value[0].Editions.Value[0].Isbn13.Should().Be("9780439554930");
            author.Books.Value[0].Editions.Value[0].Asin.Should().Be("B00JCDK5ME");
            author.Series.Value.Single().ForeignSeriesId.Should().Be("rreading-glasses:series:321");
            author.Series.Value.Single().LinkItems.Value.Should().ContainSingle();
            author.Books.Value[0].SeriesLinks.Value.Should().ContainSingle();
            author.Books.Value[0].SeriesLinks.Value[0].Series.Value.Should().BeSameAs(author.Series.Value[0]);
        }

        [Test]
        public void should_use_real_work_endpoint()
        {
            var workPayload = ExtractWorkPayload();
            MockCachedResponse("work/456", workPayload);

            var result = Subject.GetBookInfo("rreading-glasses:work:456");

            result.Item1.Should().Be("rreading-glasses:author:123");
            result.Item2.ForeignBookId.Should().Be("rreading-glasses:work:456");
            result.Item2.ForeignEditionId.Should().Be("rreading-glasses:edition:789");
            result.Item3.Select(x => x.ForeignAuthorId).Should().Equal("rreading-glasses:author:123");
        }

        [Test]
        public void should_lookup_isbn_through_redirecting_book_endpoint()
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), true, It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, _, _) =>
                {
                    request.Url.FullUri.Should().EndWith("/book/isbn/9780439554930");
                    request.AllowAutoRedirect.Should().BeTrue();
                    return JsonResponse(request, AuthorPayload);
                });

            var books = Subject.SearchByIsbn("978-0-439-55493-0");

            books.Should().ContainSingle();
            books[0].Editions.Value.Should().ContainSingle(x => x.Isbn13 == "9780439554930");
            books[0].AuthorMetadata.Value.ForeignAuthorId.Should().Be("rreading-glasses:author:123");
        }

        [Test]
        public void should_search_real_search_endpoint_then_hydrate_work()
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), true, It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, _, _) =>
                {
                    if (request.Url.FullUri.Contains("/search?q=example"))
                    {
                        return JsonResponse(request, @"[{""bookId"":789,""workId"":456,""author"":{""id"":123}}]");
                    }

                    if (request.Url.FullUri.EndsWith("/work/456"))
                    {
                        return JsonResponse(request, ExtractWorkPayload());
                    }

                    Assert.Fail("Unexpected request: " + request.Url.FullUri);
                    return null;
                });

            var books = Subject.SearchForNewBook("example", null);

            books.Should().ContainSingle();
            books[0].ForeignBookId.Should().Be("rreading-glasses:work:456");
        }

        [Test]
        public void should_return_null_when_change_feed_is_limited()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => JsonResponse(request, @"{""Limited"":true,""Ids"":[]}"));

            Subject.GetChangedAuthors(DateTime.UtcNow).Should().BeNull();
        }

        [Test]
        public void should_emit_namespaced_change_feed_ids_when_complete()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => JsonResponse(request, @"{""Limited"":false,""Ids"":[1,2,2]}"));

            Subject.GetChangedAuthors(DateTime.UtcNow)
                .Should()
                .BeEquivalentTo("rreading-glasses:author:1", "rreading-glasses:author:2");
        }

        [Test]
        public void should_surface_deleted_author_without_mutating_identity()
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, _, _) => JsonResponse(request, string.Empty, HttpStatusCode.NotFound));

            FluentActions.Invoking(() => Subject.GetAuthorInfo("rreading-glasses:author:123"))
                .Should()
                .Throw<AuthorNotFoundException>();
        }

        private void MockCachedResponse(string path, string payload)
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, _, _) =>
                {
                    request.Url.FullUri.Should().EndWith("/" + path);
                    return JsonResponse(request, payload);
                });
        }

        private static string ExtractWorkPayload()
        {
            return @"{
  ""ForeignId"": 456,
  ""Title"": ""Example Book"",
  ""FullTitle"": ""Example Book: A Novel"",
  ""Url"": ""https://source.example/work/456"",
  ""Genres"": [""Fantasy""],
  ""RatingCount"": 20,
  ""AverageRating"": 4.5,
  ""Authors"": [{ ""ForeignId"": 123, ""Name"": ""Example Author"" }],
  ""Books"": [
    {
      ""ForeignId"": 789,
      ""Title"": ""Example Book"",
      ""Isbn13"": ""9780439554930"",
      ""RatingCount"": 20,
      ""AverageRating"": 4.5,
      ""Contributors"": [{ ""ForeignId"": 123, ""Role"": ""Author"" }]
    }
  ],
  ""Series"": [
    {
      ""ForeignId"": 321,
      ""Title"": ""Example Series"",
      ""LinkItems"": [
        {
          ""ForeignWorkId"": 456,
          ""PositionInSeries"": ""1"",
          ""SeriesPosition"": 1,
          ""Primary"": true
        }
      ]
    }
  ]
}";
        }

        private static HttpResponse JsonResponse(HttpRequest request, string content, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, content, statusCode);
        }
    }
}
