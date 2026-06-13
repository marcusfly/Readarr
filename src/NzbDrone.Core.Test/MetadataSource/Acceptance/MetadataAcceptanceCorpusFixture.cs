using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;
using NzbDrone.Core.MetadataSource.RreadingGlasses;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.Acceptance
{
    [TestFixture]
    public class MetadataAcceptanceCorpusFixture : CoreTest<RreadingGlassesMetadataProvider>
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        };

        [SetUp]
        public void SetUp()
        {
            var requestBuilderFactory = new HttpRequestBuilder("https://api.bookinfo.club/v1{route}").CreateFactory();

            Mocker.GetMock<IMetadataRequestBuilder>()
                .Setup(x => x.GetRequestBuilder(MetadataRequestBuilder.RreadingGlassesProvider))
                .Returns(requestBuilderFactory);
        }

        [Test]
        public void should_map_representative_open_library_graph_with_stable_identities()
        {
            var authorResource = ReadFixture<OLAuthorResource>("OpenLibrary/author-complete.json");
            var worksResource = ReadFixture<OLAuthorWorksResource>("OpenLibrary/author-works-series.json");
            var editionsResource = ReadFixture<OLEditionsResponse>("OpenLibrary/editions-mixed.json");

            var author = BookInfoProxy.MapAuthorMetadata(authorResource, authorResource.Key);
            var book = BookInfoProxy.MapBook(worksResource.Entries[0], editionsResource.Entries, null);
            var series = BookInfoProxy.ExtractSeriesFromWorks(worksResource.Entries, authorResource.Key);

            author.ForeignAuthorId.Should().Be("openlibrary:author:OL100A");
            author.Name.Should().Be("Ursula Kroeber Le Guin");
            author.Overview.Should().Be("American novelist known for speculative fiction.");
            author.Aliases.Should().Contain("U. K. Le Guin");

            worksResource.Entries[0].Authors
                .Select(x => x.Author.Key)
                .Should()
                .Equal("/authors/OL100A", "/authors/OL102A");

            book.ForeignBookId.Should().Be("openlibrary:work:OL200W");
            book.Editions.Value.Select(x => x.ForeignEditionId).Should().Equal(
                "openlibrary:edition:OL300M",
                "openlibrary:edition:OL301M",
                "openlibrary:edition:OL302M");
            book.Editions.Value[0].Isbn13.Should().Be("9780547773742");
            book.Editions.Value[0].IsEbook.Should().BeTrue();

            series.Should().ContainSingle();
            series[0].Title.Should().Be("Earthsea");
            series[0].ForeignSeriesId.Should().Be(
                DerivedMetadataIdGenerator.Create("openlibrary", MetadataEntityType.Series, "OL100A:earthsea").ToString());
        }

        [Test]
        public void should_map_missing_optional_fields_to_safe_defaults()
        {
            var authorResource = ReadFixture<OLAuthorResource>("OpenLibrary/author-minimal.json");
            var worksResource = ReadFixture<OLAuthorWorksResource>("OpenLibrary/author-works-series.json");
            var editionsResource = ReadFixture<OLEditionsResponse>("OpenLibrary/editions-mixed.json");
            var minimalWork = worksResource.Entries[1];
            var minimalEdition = editionsResource.Entries[2];

            var author = BookInfoProxy.MapAuthorMetadata(authorResource, authorResource.Key);
            var book = BookInfoProxy.MapBook(minimalWork, new List<OLEditionResource> { minimalEdition }, null);
            var edition = book.Editions.Value.Single();

            author.ForeignAuthorId.Should().Be("openlibrary:author:OL101A");
            author.Name.Should().Be("Minimal Author");
            author.Overview.Should().BeNull();
            author.Aliases.Should().BeEmpty();
            author.Images.Should().ContainSingle();

            book.ForeignBookId.Should().Be("openlibrary:work:OL201W");
            book.Genres.Should().BeEmpty();
            book.Ratings.Should().NotBeNull();
            edition.ForeignEditionId.Should().Be("openlibrary:edition:OL302M");
            edition.Title.Should().Be("The Tombs of Atuan");
            edition.Isbn13.Should().BeNull();
            edition.PageCount.Should().Be(0);
        }

        [Test]
        public void should_normalize_direct_and_derived_identity_corpus()
        {
            var corpus = ReadFixture<IdentityCorpus>("identity-cases.json");

            foreach (var item in corpus.Direct)
            {
                Enum.TryParse(item.Entity, true, out MetadataEntityType entityType).Should().BeTrue();

                var normalizedValue = entityType == MetadataEntityType.Author ||
                                      entityType == MetadataEntityType.Work ||
                                      entityType == MetadataEntityType.Edition
                    ? item.Input.Trim('/').Split('/').Last()
                    : item.NormalizedValue;

                MetadataIdentifier.Create(item.Provider, entityType, normalizedValue)
                    .ToString()
                    .Should()
                    .Be(item.Expected);
            }

            var firstSeries = corpus.Series[0];
            var repeated = DerivedMetadataIdGenerator.Create(
                firstSeries.Provider,
                MetadataEntityType.Series,
                firstSeries.AuthorScope + ":" + firstSeries.Title.ToLowerInvariant());
            var sameIdentity = DerivedMetadataIdGenerator.Create(
                firstSeries.Provider,
                MetadataEntityType.Series,
                firstSeries.AuthorScope + ":earthsea");
            var otherScope = DerivedMetadataIdGenerator.Create(
                corpus.Series[1].Provider,
                MetadataEntityType.Series,
                corpus.Series[1].AuthorScope + ":earthsea");

            repeated.Should().Be(sameIdentity);
            repeated.Should().NotBe(otherScope);
            repeated.ToString().Should().StartWith("openlibrary:series:v1_");
        }

        [Test]
        public void should_accept_mixed_change_feed_identifier_shapes()
        {
            var payload = ReadMetadataFile("RreadingGlasses/changed-authors-mixed.json");

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => JsonResponse(request, payload, HttpStatusCode.OK));

            Subject.GetChangedAuthors(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc))
                .Should()
                .BeEquivalentTo(new[]
                {
                    "rreading-glasses:author:100",
                    "rreading-glasses:author:101",
                    "rreading-glasses:author:102",
                    "rreading-glasses:author:103",
                    "rreading-glasses:author:104"
                });
        }

        [Test]
        public void should_treat_partial_change_feed_as_unavailable()
        {
            var payload = ReadMetadataFile("RreadingGlasses/changed-authors-partial.json");

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => JsonResponse(request, payload, HttpStatusCode.OK));

            Subject.GetChangedAuthors(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc))
                .Should()
                .BeNull();
        }

        [Test]
        public void should_treat_provider_outage_as_unavailable()
        {
            var payload = ReadMetadataFile("RreadingGlasses/provider-outage.json");

            Mocker.GetMock<IHttpClient>()
                .Setup(x => x.Get(It.IsAny<HttpRequest>()))
                .Returns<HttpRequest>(request => JsonResponse(request, payload, HttpStatusCode.ServiceUnavailable));

            Subject.GetChangedAuthors(new DateTime(2026, 6, 13, 0, 0, 0, DateTimeKind.Utc))
                .Should()
                .BeNull();
        }

        private T ReadFixture<T>(string relativePath)
        {
            return JsonSerializer.Deserialize<T>(ReadMetadataFile(relativePath), SerializerOptions);
        }

        private string ReadMetadataFile(string relativePath)
        {
            return ReadAllText("Files/Metadata/" + relativePath);
        }

        private static HttpResponse JsonResponse(HttpRequest request, string content, HttpStatusCode statusCode)
        {
            return new HttpResponse(request, new HttpHeader { ContentType = "application/json" }, content, statusCode);
        }

        public sealed class IdentityCorpus
        {
            [JsonPropertyName("direct")]
            public List<DirectIdentityCase> Direct { get; set; }

            [JsonPropertyName("series")]
            public List<SeriesIdentityCase> Series { get; set; }
        }

        public sealed class DirectIdentityCase
        {
            [JsonPropertyName("provider")]
            public string Provider { get; set; }

            [JsonPropertyName("entity")]
            public string Entity { get; set; }

            [JsonPropertyName("input")]
            public string Input { get; set; }

            [JsonPropertyName("normalized_value")]
            public string NormalizedValue { get; set; }

            [JsonPropertyName("expected")]
            public string Expected { get; set; }
        }

        public sealed class SeriesIdentityCase
        {
            [JsonPropertyName("provider")]
            public string Provider { get; set; }

            [JsonPropertyName("author_scope")]
            public string AuthorScope { get; set; }

            [JsonPropertyName("title")]
            public string Title { get; set; }
        }
    }
}
