using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.CrossRequestTokenExchange
{
    public static class CryptoHelpers
    {
        /// <summary>
        /// 198 bytes, selected in advance to be the salt of the PBKDF2 operation that
        /// generates the HMAC key from the Initiator's and Issuer's keys.
        /// </summary>
        public static readonly IList<byte> FixedPbkdf2Salt = new List<byte> {
            14,95,36,20,39,141,238,191,227,141,6,169,178,222,49,181,178,228,
            159,107,80,156,94,39,57,206,35,67,79,131,29,250,189,44,170,168,
            72,25,57,74,196,231,248,231,255,249,223,241,48,143,153,241,168,237,
            43,84,123,230,241,45,85,8,210,190,183,114,45,152,25,106,158,13,
            18,97,118,36,70,193,247,155,46,117,4,215,222,199,45,90,102,126,
            23,55,172,53,186,242,220,82,94,225,80,4,74,30,113,94,42,224,
            28,70,58,157,211,92,31,18,56,234,82,35,253,40,68,19,164,124,
            150,5,252,36,236,4,31,139,141,12,130,166,255,84,55,167,166,87,
            132,252,174,231,10,203,193,129,57,80,53,195,66,251,40,217,115,246,
            71,180,238,146,73,62,26,154,246,198,207,37,170,23,126,104,43,143,
            249,125,223,211,193,144,3,56,178,218,25,238,122,63,140,171,28,223
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

        /// <summary>
        /// Sign a bearer token string using an intiator and issuer keys.
        /// </summary>
        /// <param name="initiatorKey">Intiator's key.</param>
        /// <param name="issuerKey">Issuer's key,</param>
        /// <param name="bearerToken">ASII Bearer tokento sign.</param>
        /// <returns>Signatuure of bearer token using both supplied keys.</returns>
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

        /// <summary>
        /// Generate a string for the Initiator to supply as proof it has the Initiator's
        /// key as part of the verification step, using the key strings.
        /// </summary>
        /// <param name="initiatorKey">Intiator's key.</param>
        /// <param name="issuerKey">Issuer's key,</param>
        /// <returns>Proof of possesion of the the supplied keys.</returns>
        public static string InitiatorsVerifyToken(string initiatorKey, string issuerKey)
            => InitiatorsVerifyToken(CalculateHashKey(initiatorKey, issuerKey));

        /// <summary>
        /// Generate a string for the Initiator to supply as proof it has the Initiator's
        /// key as part of the verification step, using a HMAC derived from the two keys.
        /// </summary>
        /// <param name="hmacKey">HMAC key bytes returned by CalculateHashKey.</param>
        /// <returns>Proof of possesion of the the supplied keys.</returns>
        public static string InitiatorsVerifyToken(IList<byte> hmacKey)
            => SignBearerToken(hmacKey, Encoding.ASCII.GetString(new byte[] { 1 }));

        /// <summary>
        /// Generate a string for the Issuer to supply as proof it has the Initiator's
        /// key as part of the verification step.
        /// </summary>
        /// <param name="initiatorKey">Intiator's key.</param>
        /// <param name="issuerKey">Issuer's key,</param>
        /// <returns>Proof of possesion of the the supplied keys.</returns>
        public static string IssuersVerifyToken(string initiatorKey, string issuerKey)
            => IssuersVerifyToken(CalculateHashKey(initiatorKey, issuerKey));

        /// <summary>
        /// Generate a string for the Initiator to supply as proof it has the Initiator's
        /// key as part of the verification step, using a HMAC derived from the two keys.
        /// </summary>
        /// <param name="hmacKey">HMAC key bytes returned by CalculateHashKey.</param>
        /// <returns>Proof of possesion of the the supplied keys.</returns>
        public static string IssuersVerifyToken(IList<byte> hmacKey)
            => SignBearerToken(hmacKey, Encoding.ASCII.GetString(new byte[] { 2 }));

        /// <summary>
        /// Convert an array of bytes to a string of hex.
        /// </summary>
        /// <param name="bytes">Byte array to convert.</param>
        /// <returns>Hex digits.</returns>
        private static string BytesToHex(IList<byte> bytes)
            => string.Concat(bytes.Select(b => b.ToString("X2")));
    }
}
