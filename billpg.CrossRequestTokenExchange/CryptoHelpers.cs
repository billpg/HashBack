using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.CrossRequestTokenExchange
{
    public static class CryptoHelpers
    {
        public const string ExchangeName = "CrossRequestTokenExchange";
        public const string ExchangeVersion = "CRTE-DRAFT-3";

        /// <summary>
        /// 198 bytes, selected in advance to be the salt of the PBKDF2 operation that
        /// generates the HMAC key from the Initiator's and Issuer's keys.
        /// </summary>
        public static readonly IList<byte> FixedPbkdf2Salt = new List<byte> {
            8,130,100,179,192,25,216,220,51,210,109,163,83,161,252,52,144,50,
            219,249,174,41,209,236,83,19,139,83,120,51,108,12,172,119,122,58,
            125,61,132,16,166,237,137,138,116,23,179,61,170,123,251,71,235,132,
            101,196,80,55,110,131,118,137,228,167,219,98,113,235,114,39,2,125,
            246,51,153,117,22,98,77,70,252,247,51,229,183,17,250,167,181,22,
            124,5,173,94,0,62,172,215,154,106,161,142,39,223,153,50,200,109,
            105,52,171,64,115,16,121,223,248,207,109,237,129,22,204,82,24,197,
            58,219,161,22,125,37,232,121,75,206,121,226,173,6,27,159,26,21,
            234,111,136,75,54,196,108,18,152,250,50,78,119,156,190,224,27,30,
            21,127,69,77,130,44,10,233,248,62,13,132,176,172,155,148,99,81,
            112,168,247,32,132,234,211,43,133,167,202,181,34,66,3,63,55,34
        }.AsReadOnly();

        /// <summary>
        /// Convert both the iniitator's and issuer's key strings into an actual 256-bit HMAC key.
        /// </summary>
        /// <param name="initiatorsKey">The initiator's key, supplied in the InitiateRequest.</param>
        /// <param name="issuersKey">The issuer's key, supplied in an ValidateRequest or a IssueRequest.</param>
        /// <returns>Derived HMAC key bytes.</returns> 
        public static IList<byte> CalculateHashKey(string initiatorsKey, string issuersKey)
            => System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.ASCII.GetBytes(initiatorsKey + " " + issuersKey + " "),
                salt: FixedPbkdf2Salt.ToArray(),
                iterations: 99,
                hashAlgorithm: System.Security.Cryptography.HashAlgorithmName.SHA256,
                outputLength: 256 / 8).ToList().AsReadOnly();

        /// <summary>
        /// Generate an initiator/issuer key suitable for the CRTE process.
        /// </summary>
        /// <returns>Generated initiator/issuer key.</returns>
        public static string GenerateRandomKeyString()
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

        public static string SignBearerToken(string initiatorKey, string issuerKey, string bearerToken)
            => SignBearerToken(CalculateHashKey(initiatorKey, issuerKey), bearerToken);

        public static string SignBearerToken(IList<byte> hmacKey, string bearerToken)
        {
            /* Convert the bearer token into an array of bytes. */
            byte[] bearerTokenAsBytes = Encoding.ASCII.GetBytes(bearerToken);

            /* Sign the bearer token. */
            using var hmac = new System.Security.Cryptography.HMACSHA256(hmacKey.ToArray());
            var hash = hmac.ComputeHash(bearerTokenAsBytes);

            /* Return the hash bytes in hex. */
            return BytesToHex(hash);
        }


        public static string InitiatorsVerifyToken(string initiatorKey, string issuerKey)
            => InitiatorsVerifyToken(CalculateHashKey(initiatorKey, issuerKey));

        public static string InitiatorsVerifyToken(IList<byte> hmacKey)
            => SignBearerToken(hmacKey, Encoding.ASCII.GetString(new byte[] { 1 }));

        public static string IssuersVerifyToken(string initiatorKey, string issuerKey)
            => IssuersVerifyToken(CalculateHashKey(initiatorKey, issuerKey));

        public static string IssuersVerifyToken(IList<byte> hmacKey)
            => SignBearerToken(hmacKey, Encoding.ASCII.GetString(new byte[] { 2 }));

        private static string BytesToHex(IList<byte> bytes)
            => string.Concat(bytes.Select(b => b.ToString("X2")));

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
