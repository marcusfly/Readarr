using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class FtsNormalizationFixture : TestBase
    {
        [TestCase("Brandon Sanderson's Mistborn", "brandon sanderson mistborn")]
        [TestCase("Readers’ Digest", "readers digest")]
        public void should_strip_possessives(string input, string expected)
        {
            FtsNormalization.Normalize(input).Should().Be(expected);
        }

        [TestCase("Guns & Ammo", "guns and ammo")]
        [TestCase("Research + Design", "research and design")]
        public void should_normalize_and_synonyms(string input, string expected)
        {
            FtsNormalization.Normalize(input).Should().Be(expected);
        }

        [Test]
        public void should_strip_diacritics()
        {
            FtsNormalization.Normalize("Heros & Héros").Should().Be("heros and heros");
        }

        [TestCase("Dune audiobook mp3", "dune")]
        [TestCase("Mistborn (Unabridged)", "mistborn")]
        public void should_strip_format_suffixes(string input, string expected)
        {
            FtsNormalization.Normalize(input).Should().Be(expected);
        }

        [Test]
        public void should_strip_punctuation()
        {
            FtsNormalization.Normalize("Mr. Smith, Vs. The World!").Should().Be("mr smith vs the world");
        }

        [TestCase("Brandon Sanderson's Héros + Co. audiobook")]
        [TestCase("Motor Trend - 2024-03-01")]
        [TestCase("Dr. Jekyll & Mr. Hyde")]
        public void should_be_stable_when_normalized_twice(string input)
        {
            var normalized = FtsNormalization.Normalize(input);

            FtsNormalization.Normalize(normalized).Should().Be(normalized);
        }

        [Test]
        public void should_normalize_multiple_values()
        {
            FtsNormalization.NormalizeValues(new[] { "Guns & Ammo", "Héros" })
                            .Should()
                            .Equal("guns and ammo", "heros");
        }

        [Test]
        public void should_return_empty_sequence_for_null_values()
        {
            FtsNormalization.NormalizeValues(null).Should().BeEmpty();
        }
    }
}
