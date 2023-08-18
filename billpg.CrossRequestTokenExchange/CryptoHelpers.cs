using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.CrossRequestTokenExchange
{
    public static class CryptoHelpers
    {
        /// <summary>
        /// 120 bytes, selected in advance to be the salt of the PBKDF2 operation that
        /// generates the HMAC key from the Initiator's and Issuer's keys.
        /// </summary>
        public static readonly IList<byte> FixedPbkdf2Salt = Encoding.ASCII.GetBytes(
            "EWNSJHKKHOJGAJBMKAYGKJKLMNCAAISFNKCFXJAT" +
            "YFZFYVQHLZNKHCXWEEDAIOXWXYCVOHUGSAASAICT" +
            "GMVYVATDOYXXQHNDRXXQHPXHFOSQPNPQKUWWCJUO"
            ).ToList().AsReadOnly();

        /// <summary>
        /// Convert both the iniitator's and issuer's key strings into an actual 256-bit HMAC key.
        /// </summary>
        /// <param name="initiatorsKey">The initiator's key, supplied in the InitiateRequest.</param>
        /// <param name="issuersKey">The issuer's key, supplied in an ValidateRequest or a IssueRequest.</param>
        /// <returns>Derived HMAC key bytes.</returns> 
        private static IList<byte> CalculateHashKey(string initiatorsKey, string issuersKey)
            => System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password: CombineKeyStrings(initiatorsKey, issuersKey).ToArray(),
                salt: FixedPbkdf2Salt.ToArray(),
                iterations: 99,
                hashAlgorithm: System.Security.Cryptography.HashAlgorithmName.SHA256,
                outputLength: 256 / 8).ToList().AsReadOnly();

        private static IList<byte> CombineKeyStrings(string initiatorsKey, string issuersKey)
        {
            /* Start byte collection and define functions for adding bytes. */
            var passwordBytes = new List<byte>();
            void addByte(byte b) => passwordBytes.Add(b);
            void addString(string s) => passwordBytes.AddRange(Encoding.ASCII.GetBytes(s));
            void addStringLength(string s) => addString(s.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));

            /* Add the parts of the combined password bytes as per standard. */
            addStringLength(initiatorsKey);
            addByte(32);
            addString(initiatorsKey);
            addByte(32);
            addStringLength(issuersKey);
            addByte(32);
            addString(issuersKey);
            addByte(33);

            /* Return completed bytes. */
            return passwordBytes.AsReadOnly();
        }

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

        /// <summary>
        /// Sign a bearer token string using an intiator and issuer keys.
        /// </summary>
        /// <param name="initiatorKey">Intiator's key.</param>
        /// <param name="issuerKey">Issuer's key,</param>
        /// <param name="bearerToken">ASII Bearer tokento sign.</param>
        /// <returns>Signatuure of bearer token using both supplied keys.</returns>
        public static string SignBearerToken(string initiatorKey, string issuerKey, string bearerToken)
        {
            /* Derive the hash key from the two supplied keys. */
            IList<byte> hmacKey = CalculateHashKey(initiatorKey, issuerKey);

            /* Convert the bearer token into an array of bytes. */
            byte[] bearerTokenAsBytes = Encoding.ASCII.GetBytes(bearerToken);

            /* Sign the bearer token. */
            using var hmac = new System.Security.Cryptography.HMACSHA256(hmacKey.ToArray());
            var hash = hmac.ComputeHash(bearerTokenAsBytes);

            /* Return the hash bytes in hex. */
            return BytesToHex(hash);
        }

        /// <summary>
        /// Convert an array of bytes to a string of hex.
        /// </summary>
        /// <param name="bytes">Byte array to convert.</param>
        /// <returns>Hex digits.</returns>
        private static string BytesToHex(IList<byte> bytes)
            => string.Concat(bytes.Select(b => b.ToString("X2")));
    }
}
