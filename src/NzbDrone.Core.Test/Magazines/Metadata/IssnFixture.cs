using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Magazines.Metadata;

namespace NzbDrone.Core.Test.Magazines.Metadata
{
    [TestFixture]
    public class IssnFixture
    {
        [Test]
        public void should_compute_check_digit()
        {
            Issn.ComputeCheckDigit("0951028").Should().Be('1');
        }

        [Test]
        public void should_normalize_valid_issn_values()
        {
            Issn.Normalize("2049-3630").Should().Be("2049-3630");
            Issn.Normalize("00280836").Should().Be("0028-0836");
            Issn.Normalize("0000-006X").Should().Be("0000-006X");
        }

        [Test]
        public void should_validate_known_issn_values()
        {
            Issn.IsValid("2049-3630").Should().BeTrue();
            Issn.IsValid("0028-0836").Should().BeTrue();
            Issn.IsValid("0000-006X").Should().BeTrue();
        }

        [Test]
        public void should_reject_invalid_issn_values()
        {
            Issn.IsValid("2049-3631").Should().BeFalse();
            Issn.IsValid("2049-36").Should().BeFalse();
            Issn.IsValid("bad-value").Should().BeFalse();
            Issn.Normalize("2049-3631").Should().BeNull();
        }

        [Test]
        public void should_decode_977_ean13_to_issn()
        {
            Issn.FromEan13("9770951028330").Should().Be("0951-0281");
        }

        [Test]
        public void should_reject_invalid_ean13_values()
        {
            Issn.FromEan13("9780951028330").Should().BeNull();
            Issn.FromEan13("977095102833").Should().BeNull();
        }
    }
}
