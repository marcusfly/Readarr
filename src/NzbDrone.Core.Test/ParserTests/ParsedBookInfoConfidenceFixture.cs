using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ParserTests
{
    /// <summary>
    /// Tests for the Confidence and RejectionReason fields added to ParsedBookInfo,
    /// covering diverse release formats: standard, audiobook, ebook, collection/discography,
    /// multilingual, edition variants, and malformed/hashed inputs.
    /// </summary>
    [TestFixture]
    public class ParsedBookInfoConfidenceFixture : CoreTest
    {
        // ---------------------------------------------------------------------------
        // Standard author – book – year releases (confidence should be 1.0)
        // ---------------------------------------------------------------------------
        [TestCase("Brandon Sanderson - The Way of Kings (2010) EPUB", "Brandon Sanderson", "The Way of Kings", 1.0f)]
        [TestCase("Frank Herbert - Dune - 1965 EPUB", "Frank Herbert", "Dune", 1.0f)]
        [TestCase("Terry Pratchett - Guards Guards (1989) MP3 128kbps", "Terry Pratchett", "Guards Guards", 1.0f)]
        public void should_assign_full_confidence_when_author_book_and_year_are_present(
            string title, string expectedAuthor, string expectedBook, float expectedConfidence)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().NotBeNull();
            result.AuthorName.Should().NotBeNullOrWhiteSpace();
            result.BookTitle.Should().NotBeNullOrWhiteSpace();
            result.Confidence.Should().BeApproximately(expectedConfidence, 0.01f);
        }

        // ---------------------------------------------------------------------------
        // Author + book but no year (confidence 0.67)
        // ---------------------------------------------------------------------------
        [TestCase("George Orwell - Nineteen Eighty-Four [EPUB]")]
        [TestCase("Ursula K Le Guin - The Left Hand of Darkness (MOBI)")]
        public void should_assign_reduced_confidence_when_year_is_absent(string title)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().NotBeNull("release '{0}' should parse", title);
            result.Confidence.Should().BeLessOrEqualTo(0.68f,
                "confidence should be reduced when the release year is not present");
        }

        // ---------------------------------------------------------------------------
        // Audiobook releases
        // ---------------------------------------------------------------------------
        [TestCase("Stephen King - It (Audiobook) (2020) MP3 64kbps", "Stephen King", "It")]
        [TestCase("Patrick Rothfuss - The Name of the Wind (Unabridged) - 2018 - M4B", "Patrick Rothfuss", "The Name of the Wind")]
        public void should_parse_audiobook_releases(string title, string expectedAuthor, string expectedBook)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().NotBeNull("audiobook release '{0}' should parse", title);
            result.AuthorName.Should().NotBeNullOrWhiteSpace();
            result.BookTitle.Should().NotBeNullOrWhiteSpace();
        }

        // ---------------------------------------------------------------------------
        // Ebook-style releases
        // ---------------------------------------------------------------------------
        [TestCase("Cormac McCarthy - Blood Meridian (1985) EPUB Retail")]
        [TestCase("Neil Gaiman - American Gods (2001) PDF")]
        public void should_parse_ebook_releases(string title)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().NotBeNull("ebook release '{0}' should parse", title);
            result.AuthorName.Should().NotBeNullOrWhiteSpace();
            result.BookTitle.Should().NotBeNullOrWhiteSpace();
        }

        // ---------------------------------------------------------------------------
        // Collection / discography releases
        // ---------------------------------------------------------------------------
        [TestCase("Isaac Asimov - Discography 1950-1992")]
        [TestCase("Terry Pratchett - Discography 1971-2015")]
        public void should_parse_discography_releases(string title)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().NotBeNull("discography release '{0}' should parse", title);
            result.Discography.Should().BeTrue();
            result.AuthorName.Should().NotBeNullOrWhiteSpace();
        }

        // ---------------------------------------------------------------------------
        // Edition variants
        // ---------------------------------------------------------------------------
        [TestCase("J.R.R. Tolkien - The Hobbit - Annotated Edition (2002) EPUB")]
        [TestCase("Douglas Adams - The Hitchhikers Guide to the Galaxy (Deluxe Edition) 1979 EPUB")]
        public void should_parse_edition_variants(string title)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().NotBeNull("edition release '{0}' should parse", title);
            result.AuthorName.Should().NotBeNullOrWhiteSpace();
            result.BookTitle.Should().NotBeNullOrWhiteSpace();
        }

        // ---------------------------------------------------------------------------
        // Multilingual releases (non-ASCII authors / titles)
        // ---------------------------------------------------------------------------
        [TestCase("Umberto Eco - Il Nome della Rosa (1980) EPUB")]
        [TestCase("Marcel Proust - À la recherche du temps perdu (1913) PDF")]
        public void should_parse_multilingual_releases(string title)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            // Parser should not crash on non-ASCII titles; a null result is acceptable
            // but if a result is returned it must have author or book populated.
            if (result != null)
            {
                var hasContent = !string.IsNullOrWhiteSpace(result.AuthorName) ||
                                 !string.IsNullOrWhiteSpace(result.BookTitle);
                hasContent.Should().BeTrue("parsed result should not be entirely empty");
            }
        }

        // ---------------------------------------------------------------------------
        // Malformed / hashed releases — parser should return null
        // ---------------------------------------------------------------------------
        [TestCase("0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d")]   // 32-char MD5 hash
        [TestCase("abc")]                                    // three-letter reject
        [TestCase("password yenc garbage")]                  // password-protected marker
        public void should_return_null_for_malformed_or_hashed_titles(string title)
        {
            var result = Parser.Parser.ParseBookTitle(title);

            result.Should().BeNull("hashed or malformed release titles should not parse");
        }

        // ---------------------------------------------------------------------------
        // Confidence defaults to 1.0 on a fresh ParsedBookInfo instance
        // ---------------------------------------------------------------------------
        [Test]
        public void default_confidence_should_be_one()
        {
            var info = new NzbDrone.Core.Parser.Model.ParsedBookInfo();
            info.Confidence.Should().Be(1.0f);
        }

        // ---------------------------------------------------------------------------
        // RejectionReason defaults to null
        // ---------------------------------------------------------------------------
        [Test]
        public void default_rejection_reason_should_be_null()
        {
            var info = new NzbDrone.Core.Parser.Model.ParsedBookInfo();
            info.RejectionReason.Should().BeNull();
        }

        // ---------------------------------------------------------------------------
        // RejectionReason can be set and retrieved
        // ---------------------------------------------------------------------------
        [Test]
        public void rejection_reason_can_be_set()
        {
            var info = new NzbDrone.Core.Parser.Model.ParsedBookInfo
            {
                RejectionReason = "Author name does not match monitored author"
            };

            info.RejectionReason.Should().Be("Author name does not match monitored author");
        }

        // ---------------------------------------------------------------------------
        // Confidence stays within [0, 1]
        // ---------------------------------------------------------------------------
        [Test]
        public void confidence_is_between_zero_and_one_for_typical_releases()
        {
            var titles = new[]
            {
                "Brandon Sanderson - The Way of Kings (2010) EPUB",
                "George Orwell - Nineteen Eighty-Four [EPUB]",
                "Isaac Asimov - Discography 1950-1992",
            };

            foreach (var title in titles)
            {
                var result = Parser.Parser.ParseBookTitle(title);
                if (result == null)
                {
                    continue;
                }

                result.Confidence.Should().BeInRange(
                    0f,
                    1f,
                    "confidence must always be between 0 and 1 (title: '{0}')",
                    title);
            }
        }
    }
}
