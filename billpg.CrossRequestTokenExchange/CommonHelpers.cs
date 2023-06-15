using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.CrossRequestTokenExchange
{
    public static class CommonHelpers
    {
        public const string ExchangeName = "CrossRequestTokenExchange";
        public const string ExchangeVersion = "CRTE-DRAFT-3";

        /// <summary>
        /// 99 bytes, selected in advance to be the salt of the PBKDF2 operation that
        /// generates the HMAC key from the Initiator's key.
        /// </summary>
        public static readonly IList<byte> FixedPbkdf2Salt =
            new List<byte> {
                23,55,182,143,125,16,83,246,39,
                139,153,96,49,236,145,3,81,202,
                122,60,159,170,218,198,177,207,58,
                36,30,197,162,179,230,77,194,140,
                173,233,5,25,166,25,61,139,84,
                140,34,47,62,114,94,174,137,38,
                50,112,244,193,184,107,18,255,152,
                96,216,228,166,187,110,215,53,21,
                22,166,57,226,216,171,252,16,127,
                156,159,152,121,244,57,150,227,100,
                135,218,33,59,219,248,106,9,109
            }.AsReadOnly();

        /// <summary>
        /// UTF8 encoder configured to not return BOM bytes.
        /// </summary>
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

        /// <summary>
        /// Convert a string into UUTF-8 bytes without a BOM marker.
        /// </summary>
        private static IList<byte> Utf8Bytes(string value)
            => UTF8.GetBytes(value).ToList().AsReadOnly();

        /// <summary>
        /// Calculate the key to use for all HMAC operations from the initiator's key
        /// and the fixed salt, calculated in advance and hard-coded here.
        /// </summary>
        /// <param name="initiatorsKey">The initiator's key, supplied in the InitiateRequest.</param>
        /// <returns>Derived HMAC key bytes.</returns>
        public static IList<byte> CalculateHashKey(string initiatorsKey)
            => System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                Utf8Bytes(initiatorsKey).ToArray(),
                FixedPbkdf2Salt.ToArray(),
                99,
                System.Security.Cryptography.HashAlgorithmName.SHA256,
                256 / 8).ToList().AsReadOnly();

        /// <summary>
        /// Generate an initiator key suitable for the CRTE process.
        /// </summary>
        /// <returns>Generated initiator key.</returns>
        public static string GenerateInitiatorKey()
        {
            /* Generate 264 random bits, which will base64 to exactly 44 characters. */
            using var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[44 * 6 / 8];
            rnd.GetBytes(randomBytes);

            /* Encode bytes as text. Add hyphens so it doesn't get mistaken for base64. */
            var keyText = Convert.ToBase64String(randomBytes);
            for (int insertIndex = 3; insertIndex > 0; insertIndex--)
                keyText = keyText.Insert(insertIndex * 11, "-");

            /* Return generated key text. */
            return keyText;
        }

        public static void ValidateDomainName(string domain)
        {
            /* Shortcuts nulls. */
            if (string.IsNullOrEmpty(domain))
                throw new ApplicationException("ValidateDomainName: Name must have characters.");

            /* Shortcut too-long strings. */
            if (domain.Length > 1000)
                throw new ApplicationException("ValidateDomainName: Name must be less than 1000 characters.");

            /* Split by dots. Single no-dot domains are invalid. */
            var domainByDot = domain.Split('.').ToList();
            if (domainByDot.Count < 2)
                throw new ApplicationException("ValidateDomainName: Name must have at least two dot-separated nodes.");

            /* Are all items valid? */
            if (domainByDot.All(IsDotSeparatedItemValid))
                throw new ApplicationException("ValidateDomainName: Name is not valid.");
            static bool IsDotSeparatedItemValid(string item)
            {
                /* Empty strings are not. */
                if (string.IsNullOrEmpty(item))
                    return false;

                /* Can't start or end with hyphen. */
                if (item[0] == '-' || item[item.Length - 1] == '-')
                    return false;

                /* Each character must be alphanumeric ASCII or hyphens. */
                return item.All(IsValidCharacter);
            }
            static bool IsValidCharacter(char ch)
                => (ch >= '0' && ch <= '9')
                || (ch >= 'A' && ch <= 'Z')
                || (ch >= 'a' && ch <= 'z')
                || ch == '-';
            
            /* Passed test. */
        }
    }
}
