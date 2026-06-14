using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MetadataSource.Contracts;
using NzbDrone.Core.MetadataSource.Identity;

namespace NzbDrone.Core.Test.MetadataSource.Contract
{
    [TestFixture]
    public class MetadataIdentifierFixture
    {
        [Test]
        public void create_should_normalize_provider_and_isbn_value()
        {
            var identifier = MetadataIdentifier.Create(" OpenLibrary ", MetadataEntityType.Isbn, "978-0-439-55493-0");

            identifier.Provider.Should().Be("openlibrary");
            identifier.EntityType.Should().Be(MetadataEntityType.Isbn);
            identifier.Value.Should().Be("9780439554930");
            identifier.ToString().Should().Be("openlibrary:isbn:9780439554930");
        }

        [Test]
        public void create_should_normalize_asin_to_uppercase()
        {
            var identifier = MetadataIdentifier.Create("amazon", MetadataEntityType.Asin, " b00jcdk5me ");

            identifier.Value.Should().Be("B00JCDK5ME");
            identifier.ToString().Should().Be("amazon:asin:B00JCDK5ME");
        }

        [Test]
        public void parse_should_round_trip_normalized_identifier()
        {
            var identifier = MetadataIdentifier.Parse("OpenLibrary:Author:OL23919A");

            identifier.Provider.Should().Be("openlibrary");
            identifier.EntityType.Should().Be(MetadataEntityType.Author);
            identifier.Value.Should().Be("OL23919A");
            identifier.ToString().Should().Be("openlibrary:author:OL23919A");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("provider")]
        [TestCase("provider:author")]
        [TestCase("provider:unknown:value")]
        [TestCase("provider:author:value:extra")]
        [TestCase("provider:asin:short")]
        [TestCase("provider:isbn:123")]
        [TestCase("bad key:author:value")]
        public void try_parse_should_reject_invalid_formats(string value)
        {
            MetadataIdentifier.TryParse(value, out var identifier).Should().BeFalse();
            identifier.IsEmpty.Should().BeTrue();
        }

        [Test]
        public void parse_should_throw_for_invalid_value()
        {
            FluentActions.Invoking(() => MetadataIdentifier.Parse("provider:isbn:123"))
                         .Should()
                         .Throw<FormatException>();
        }

        [Test]
        public void equality_should_use_normalized_components()
        {
            var left = MetadataIdentifier.Create("OpenLibrary", MetadataEntityType.Isbn, "978-0-439-55493-0");
            var right = MetadataIdentifier.Parse("openlibrary:isbn:9780439554930");

            left.Should().Be(right);
            left.GetHashCode().Should().Be(right.GetHashCode());
        }

        [Test]
        public void non_isbn_values_should_preserve_case()
        {
            var identifier = MetadataIdentifier.Create("openlibrary", MetadataEntityType.Work, "OL123W");

            identifier.Value.Should().Be("OL123W");
            identifier.ToString().Should().Be("openlibrary:work:OL123W");
        }

        [Test]
        public void edition_match_key_should_prefer_normalized_isbn_over_provider_id()
        {
            var left = new Edition
            {
                ForeignEditionId = "rreading-glasses:edition:1",
                Isbn13 = "978-0-439-55493-0"
            };
            var right = new Edition
            {
                ForeignEditionId = "openlibrary:edition:OL1M",
                Isbn13 = "9780439554930"
            };

            MetadataEditionIdentity.GetMatchKey(left)
                .Should()
                .Be(MetadataEditionIdentity.GetMatchKey(right));
        }
    }
}
