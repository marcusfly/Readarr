using System;
using System.Linq;

namespace NzbDrone.Core.Magazines.Metadata
{
    public static class Issn
    {
        public static char ComputeCheckDigit(string sevenDigits)
        {
            if (sevenDigits == null || sevenDigits.Length != 7 || sevenDigits.Any(c => !char.IsDigit(c)))
            {
                throw new ArgumentException("ISSN base must contain exactly seven digits.", nameof(sevenDigits));
            }

            var sum = 0;
            for (var i = 0; i < sevenDigits.Length; i++)
            {
                sum += (sevenDigits[i] - '0') * (8 - i);
            }

            var check = (11 - (sum % 11)) % 11;
            return check == 10 ? 'X' : (char)('0' + check);
        }

        public static bool IsValid(string issn)
        {
            return Normalize(issn) != null;
        }

        public static string Normalize(string issn)
        {
            if (string.IsNullOrWhiteSpace(issn))
            {
                return null;
            }

            var value = issn.Trim().Replace(" ", string.Empty);
            if (value.Length == 9 && value[4] == '-')
            {
                value = value.Replace("-", string.Empty);
            }
            else if (value.Contains("-"))
            {
                return null;
            }

            if (value.Length != 8 || value.Take(7).Any(c => !char.IsDigit(c)))
            {
                return null;
            }

            var sevenDigits = value.Substring(0, 7);
            var checkDigit = value[7];
            if (!char.IsDigit(checkDigit) && char.ToUpperInvariant(checkDigit) != 'X')
            {
                return null;
            }

            var expected = ComputeCheckDigit(sevenDigits);
            if (char.ToUpperInvariant(checkDigit) != expected)
            {
                return null;
            }

            return $"{sevenDigits.Substring(0, 4)}-{sevenDigits.Substring(4, 3)}{expected}";
        }

        public static string FromEan13(string ean)
        {
            if (string.IsNullOrWhiteSpace(ean))
            {
                return null;
            }

            var value = ean.Trim();
            if (value.Length != 13 || value.Take(13).Any(c => !char.IsDigit(c)) || !value.StartsWith("977", StringComparison.Ordinal))
            {
                return null;
            }

            var baseDigits = value.Substring(3, 7);
            var checkDigit = ComputeCheckDigit(baseDigits);
            return $"{baseDigits.Substring(0, 4)}-{baseDigits.Substring(4, 3)}{checkDigit}";
        }
    }
}
