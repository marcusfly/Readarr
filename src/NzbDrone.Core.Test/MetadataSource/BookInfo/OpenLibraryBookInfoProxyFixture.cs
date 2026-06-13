using System;
using System.Collections.Generic;
using System.Net;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Http;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.BookInfo
{
    [TestFixture]
    public class OpenLibraryBookInfoProxyFixture : CoreTest<BookInfoProxy>
    {
        private const string AuthorOlid = "OL1A";
        private const string WorkOlid = "OL1W";
        private const string EditionOlid = "OL1M";
        private const string Isbn13 = "9780439554930";

        [SetUp]
        public void SetUp()
        {
            var requestBuilderFactory = new HttpRequestBuilder("https://openlibrary.org").CreateFactory();

            Mocker.GetMock<IMetadataRequestBuilder>()
                .Setup(x => x.GetRequestBuilder())
                .Returns(requestBuilderFactory);

            Mocker.GetMock<IMetadataRequestBuilder>()
                .Setup(x => x.GetRequestBuilder(It.IsAny<string>()))
                .Returns(requestBuilderFactory);

            Mocker.GetMock<IAuthorService>()
                .Setup(x => x.FindById(It.IsAny<string>()))
                .Returns((Author)null);

            Mocker.GetMock<IBookService>()
                .Setup(x => x.FindById(It.IsAny<string>()))
                .Returns((Book)null);
        }

        [Test]
        public void should_emit_namespaced_author_work_and_edition_ids()
        {
            var authorMetadata = BookInfoProxy.MapAuthorMetadata(new OLAuthorResource
            {
                Name = "Example Author"
            }, AuthorOlid);

            var book = BookInfoProxy.MapBook(new OLWorkResource
            {
                Key = "/works/" + WorkOlid,
                Title = "Example Book"
            }, new List<OLEditionResource>
            {
                new OLEditionResource
                {
                    Key = "/books/" + EditionOlid,
                    Title = "Example Book",
                    Isbn13 = new List<string> { Isbn13 }
                }
            }, new Ratings());

            authorMetadata.ForeignAuthorId.Should().Be(MetadataIdentifier.Create("openlibrary", MetadataEntityType.Author, AuthorOlid).ToString());
            book.ForeignBookId.Should().Be(MetadataIdentifier.Create("openlibrary", MetadataEntityType.Work, WorkOlid).ToString());
            book.Editions.Value.Should().ContainSingle();
            book.Editions.Value[0].ForeignEditionId.Should().Be(MetadataIdentifier.Create("openlibrary", MetadataEntityType.Edition, EditionOlid).ToString());
        }

        [Test]
        public void should_keep_edition_identity_bound_to_olid_even_with_isbn()
        {
            var edition = BookInfoProxy.MapEdition(new OLEditionResource
            {
                Key = "/books/" + EditionOlid,
                Title = "Example Edition",
                Isbn13 = new List<string> { Isbn13 }
            }, new OLWorkResource
            {
                Key = "/works/" + WorkOlid,
                Title = "Example Book"
            });

            edition.ForeignEditionId.Should().Be(MetadataIdentifier.Create("openlibrary", MetadataEntityType.Edition, EditionOlid).ToString());
            edition.TitleSlug.Should().Be(edition.ForeignEditionId);
            edition.Isbn13.Should().Be(Isbn13);
        }

        [Test]
        public void should_generate_deterministic_series_ids()
        {
            var works = new List<OLWorkResource>
            {
                new OLWorkResource
                {
                    Key = "/works/OL2W",
                    SeriesList = new List<string> { "The Saga #1" }
                },
                new OLWorkResource
                {
                    Key = "/works/OL3W",
                    SeriesList = new List<string> { "  the   saga  #2 " }
                }
            };

            var first = BookInfoProxy.ExtractSeriesFromWorks(works, AuthorOlid);
            var second = BookInfoProxy.ExtractSeriesFromWorks(works, AuthorOlid);
            var expected = DerivedMetadataIdGenerator.Create("openlibrary", MetadataEntityType.Series, AuthorOlid + ":the saga").ToString();

            first.Should().ContainSingle();
            second.Should().ContainSingle();
            first[0].ForeignSeriesId.Should().Be(expected);
            second[0].ForeignSeriesId.Should().Be(expected);
        }

        [TestCase("work", WorkOlid)]
        [TestCase("work", "openlibrary-work")]
        [TestCase("edition", EditionOlid)]
        [TestCase("edition", "openlibrary-edition")]
        public void should_accept_raw_and_namespaced_ids_in_search_prefixes(string prefix, string identifierKey)
        {
            var namespacedWorkId = MetadataIdentifier.Create("openlibrary", MetadataEntityType.Work, WorkOlid).ToString();
            var namespacedEditionId = MetadataIdentifier.Create("openlibrary", MetadataEntityType.Edition, EditionOlid).ToString();
            var term = prefix + ":" + (identifierKey == "openlibrary-work" ? namespacedWorkId : identifierKey == "openlibrary-edition" ? namespacedEditionId : identifierKey);

            MockAuthorAndWorkGraph();

            var result = Subject.SearchForNewBook(term, null, false);

            result.Should().ContainSingle();
            result[0].ForeignBookId.Should().Be(namespacedWorkId);
            result[0].Editions.Value.Should().ContainSingle();
            result[0].Editions.Value[0].ForeignEditionId.Should().Be(namespacedEditionId);
            result[0].Editions.Value[0].Monitored.Should().BeTrue();
            Mocker.GetMock<IAuthorService>().Verify(x => x.FindById(MetadataIdentifier.Create("openlibrary", MetadataEntityType.Author, AuthorOlid).ToString()), Times.AtLeastOnce());
        }

        [Test]
        public void should_accept_raw_and_namespaced_ids_in_public_methods()
        {
            var namespacedAuthorId = MetadataIdentifier.Create("openlibrary", MetadataEntityType.Author, AuthorOlid).ToString();
            var namespacedWorkId = MetadataIdentifier.Create("openlibrary", MetadataEntityType.Work, WorkOlid).ToString();

            MockAuthorAndWorkGraph();

            Subject.GetAuthorInfo(AuthorOlid).ForeignAuthorId.Should().Be(namespacedAuthorId);
            Subject.GetAuthorInfo(namespacedAuthorId).ForeignAuthorId.Should().Be(namespacedAuthorId);

            Subject.GetBookInfo(WorkOlid).Item2.ForeignBookId.Should().Be(namespacedWorkId);
            Subject.GetBookInfo(namespacedWorkId).Item2.ForeignBookId.Should().Be(namespacedWorkId);
        }

        [Test]
        public void should_stop_after_three_429_retries_for_author_requests()
        {
            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, useCache, ttl) => new HttpResponse(
                    request,
                    new HttpHeader { { "Retry-After", "0" } },
                    string.Empty,
                    HttpStatusCode.TooManyRequests));

            Assert.Throws<BookInfoException>(() => Subject.GetAuthorInfo(AuthorOlid, false));

            Mocker.GetMock<ICachedHttpResponseService>()
                .Verify(x => x.Get(It.Is<HttpRequest>(r => r.Url.FullUri.EndsWith("/authors/" + AuthorOlid + ".json")), It.IsAny<bool>(), It.IsAny<TimeSpan>()), Times.Exactly(3));
        }

        private void MockAuthorAndWorkGraph()
        {
            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.Is<HttpRequest>(r => r.Url.FullUri.EndsWith("/books/" + EditionOlid + ".json"))))
                .Returns<HttpRequest>(request => JsonResponse(request, @"{
  ""key"": ""/books/OL1M"",
  ""title"": ""Example Book"",
  ""isbn_13"": [""9780439554930""],
  ""works"": [{ ""key"": ""/works/OL1W"" }]
}"));

            Mocker.GetMock<ICachedHttpResponseService>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>(), It.IsAny<bool>(), It.IsAny<TimeSpan>()))
                .Returns<HttpRequest, bool, TimeSpan>((request, useCache, ttl) =>
                {
                    if (request.Url.FullUri.EndsWith("/works/" + WorkOlid + ".json"))
                    {
                        return JsonResponse(request, @"{
  ""key"": ""/works/OL1W"",
  ""title"": ""Example Book"",
  ""authors"": [{ ""author"": { ""key"": ""/authors/OL1A"" } }]
}");
                    }

                    if (request.Url.FullUri.EndsWith("/works/" + WorkOlid + "/editions.json?limit=50"))
                    {
                        return JsonResponse(request, @"{
  ""entries"": [
    {
      ""key"": ""/books/OL1M"",
      ""title"": ""Example Book"",
      ""isbn_13"": [""9780439554930""]
    }
  ],
  ""links"": {}
}");
                    }

                    if (request.Url.FullUri.EndsWith("/works/" + WorkOlid + "/ratings.json"))
                    {
                        return JsonResponse(request, @"{
  ""summary"": { ""average"": 4.5, ""count"": 12 }
}");
                    }

                    if (request.Url.FullUri.EndsWith("/authors/" + AuthorOlid + ".json"))
                    {
                        return JsonResponse(request, @"{
  ""key"": ""/authors/OL1A"",
  ""name"": ""Example Author""
}");
                    }

                    if (request.Url.FullUri.EndsWith("/authors/" + AuthorOlid + "/works.json?limit=50"))
                    {
                        return JsonResponse(request, @"{
  ""entries"": [],
  ""links"": {}
}");
                    }

                    Assert.Fail("Unexpected request: " + request.Url.FullUri);
                    return null;
                });
        }

        private static HttpResponse JsonResponse(HttpRequest request, string content)
        {
            return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, content);
        }
    }
}
