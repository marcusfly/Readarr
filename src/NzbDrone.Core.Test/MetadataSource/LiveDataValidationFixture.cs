using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Books;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Http;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;
using NzbDrone.Core.MetadataSource.RreadingGlasses;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class LiveDataValidationFixture : CoreTest<RreadingGlassesMetadataProvider>
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false
        };

        [SetUp]
        public void SetUp()
        {
            var requestBuilderFactory = new HttpRequestBuilder("https://hardcover.bookinfo.pro/{route}").CreateFactory();

            Mocker.GetMock<IMetadataRequestBuilder>()
                .Setup(x => x.GetRequestBuilder(MetadataRequestBuilder.RreadingGlassesProvider))
                .Returns(requestBuilderFactory);
        }

        [Test]
        [Description("Validates real Terry Pratchett data from production rreading-glasses endpoint")]
        public void should_parse_real_terry_pratchett_author_data()
        {
            var authorJson = ReadMetadataFile("RreadingGlasses/author-terry-pratchett-slim.json");
            var author = JsonSerializer.Deserialize<RgAuthorResource>(authorJson, SerializerOptions);

            // Validate author identity and metadata
            author.ForeignId.Should().Be(227859);
            author.Name.Should().Be("Terry Pratchett");
            author.Url.Should().StartWith("https://hardcover.app");
            author.ImageUrl.Should().NotBeNullOrEmpty();

            // Validate works were fetched
            author.Works.Should().NotBeEmpty();
            author.Works.Count.Should().BeLessThanOrEqualTo(10);

            // Validate first work structure
            var firstWork = author.Works.First();
            firstWork.ForeignId.Should().BeGreaterThan(0);
            firstWork.Title.Should().NotBeNullOrEmpty();
            firstWork.Books.Should().NotBeEmpty();

            // Validate edition structure
            var firstEdition = firstWork.Books.First();
            firstEdition.ForeignId.Should().BeGreaterThan(0);
            firstEdition.Title.Should().NotBeNullOrEmpty();

            // Some editions should have ISBNs
            var editionsWithIsbn = firstWork.Books.Where(b => !string.IsNullOrWhiteSpace(b.Isbn13)).ToList();
            editionsWithIsbn.Should().NotBeEmpty();

            // Validate ISBN format
            foreach (var edition in editionsWithIsbn)
            {
                edition.Isbn13.Should().MatchRegex(@"^\d{13}$", $"ISBN should be 13 digits: {edition.Isbn13}");
            }
        }

        [Test]
        [Description("Validates search results from production rreading-glasses endpoint")]
        public void should_parse_real_good_omens_search_results()
        {
            var searchJson = ReadMetadataFile("RreadingGlasses/search-good-omens.json");
            var results = JsonSerializer.Deserialize<List<RgSearchResultResource>>(searchJson, SerializerOptions);

            results.Should().NotBeNull();
            results.Should().NotBeEmpty();
            results.Should().HaveLessThanOrEqualTo(20);

            // Validate result structure
            foreach (var result in results)
            {
                result.BookId.Should().BeGreaterThan(0);
                result.WorkId.Should().BeGreaterThan(0);
                result.Author?.Id.Should().BeGreaterThan(0);
            }
        }

        [Test]
        [Description("Validates change feed response from production rreading-glasses endpoint")]
        public void should_parse_limited_change_feed_response()
        {
            var feedJson = ReadMetadataFile("RreadingGlasses/change-feed-sample.json");
            var response = JsonSerializer.Deserialize<RgChangesFeedResource>(feedJson, SerializerOptions);

            response.Should().NotBeNull();
            response.Limited.Should().BeTrue();
            response.Ids.Should().NotBeNull();
        }

        [Test]
        [Description("Validates real OpenLibrary author data")]
        public void should_parse_real_terry_pratchett_from_openlibrary()
        {
            var authorJson = ReadMetadataFile("OpenLibrary/author-terry-pratchett.json");
            var author = JsonSerializer.Deserialize<OLAuthorResource>(authorJson, SerializerOptions);

            author.Should().NotBeNull();
            author.Key.Should().Be("/authors/OL25712A");
            author.Name.Should().Contain("Terry");
            author.BirthDate.Should().NotBeNullOrEmpty();
            author.DeathDate.Should().NotBeNullOrEmpty();
            author.AlternateNames.Should().NotBeEmpty();
        }

        [Test]
        [Description("Validates real OpenLibrary search results")]
        public void should_parse_real_good_omens_search_from_openlibrary()
        {
            var searchJson = ReadMetadataFile("OpenLibrary/search-good-omens.json");
            var response = JsonSerializer.Deserialize<OLSearchResponse>(searchJson, SerializerOptions);

            response.Should().NotBeNull();
            response.NumFound.Should().BeGreaterThan(0);
            response.Docs.Should().NotBeEmpty();

            // Validate first result
            var firstResult = response.Docs.First();
            firstResult.Key.Should().StartWith("/works/");
            firstResult.Title.Should().NotBeNullOrEmpty();
        }

        [Test]
        [Description("Validates identity generation from real data")]
        public void should_generate_stable_identities_from_live_data()
        {
            // Use the slim Terry Pratchett data
            var authorJson = ReadMetadataFile("RreadingGlasses/author-terry-pratchett-slim.json");
            var author = JsonSerializer.Deserialize<RgAuthorResource>(authorJson, SerializerOptions);

            // Generate identities
            var authorId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Author, author.ForeignId.ToString());
            authorId.ToString().Should().Be("rreading-glasses:author:227859");

            // Work identity
            var work = author.Works.First();
            var workId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Work, work.ForeignId.ToString());
            workId.ToString().Should().StartWith("rreading-glasses:work:");

            // Edition identity
            var edition = work.Books.First();
            var editionId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Edition, edition.ForeignId.ToString());
            editionId.ToString().Should().StartWith("rreading-glasses:edition:");

            // ISBN identity (when available)
            if (!string.IsNullOrWhiteSpace(edition.Isbn13))
            {
                var isbnId = MetadataIdentifier.Create("rreading-glasses", MetadataEntityType.Isbn, edition.Isbn13);
                isbnId.ToString().Should().StartWith("rreading-glasses:isbn:");
            }
        }

        [Test]
        [Description("Validates multilingual edition handling from real data")]
        public void should_handle_multilingual_editions_correctly()
        {
            var authorJson = ReadMetadataFile("RreadingGlasses/author-terry-pratchett-slim.json");
            var author = JsonSerializer.Deserialize<RgAuthorResource>(authorJson, SerializerOptions);

            var multilingualEditions = author.Works
                .SelectMany(w => w.Books)
                .GroupBy(b => b.Title)
                .Where(g => g.DistinctBy(b => b.Language).Count() > 1)
                .ToList();

            // The Unadulterated Cat appears in English and Russian
            multilingualEditions.Should().NotBeEmpty();

            foreach (var group in multilingualEditions)
            {
                var languages = group.Select(b => b.Language).Distinct().ToList();
                languages.Count.Should().BeGreaterThan(1);
            }
        }

        private string ReadMetadataFile(string relativePath)
        {
            return ReadAllText("Files/Metadata/" + relativePath);
        }

        // DTO support classes for deserialization
        public sealed class RgSearchResultResource
        {
            [System.Text.Json.Serialization.JsonPropertyName("bookId")]
            public int BookId { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("workId")]
            public int WorkId { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("author")]
            public RgAuthorRefResource Author { get; set; }
        }

        public sealed class RgAuthorRefResource
        {
            [System.Text.Json.Serialization.JsonPropertyName("id")]
            public int Id { get; set; }
        }

        public sealed class RgChangesFeedResource
        {
            [System.Text.Json.Serialization.JsonPropertyName("Limited")]
            public bool Limited { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("Ids")]
            public List<int> Ids { get; set; }
        }

        public sealed class OLSearchResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("numFound")]
            public int NumFound { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("docs")]
            public List<OLSearchDoc> Docs { get; set; }
        }

        public sealed class OLSearchDoc
        {
            [System.Text.Json.Serialization.JsonPropertyName("key")]
            public string Key { get; set; }

            [System.Text.Json.Serialization.JsonPropertyName("title")]
            public string Title { get; set; }
        }
    }
}
