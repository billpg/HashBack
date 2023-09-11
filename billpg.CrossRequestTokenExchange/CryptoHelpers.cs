using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace billpg.CrossRequestTokenExchange
{
    public static class CryptoHelpers
    {
        /// <summary>
        /// Generate an HMAC key suitable for the CRTE process.
        /// </summary>
        /// <returns>Generated HMAC key in BASE64.</returns>
        public static string GenerateHmacKey()
        {
            /* Generate 256 cryptographic quality random bits into a byte block. */
            using var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[256 / 8];
            rnd.GetBytes(randomBytes);

            /* Encode those bytes as BASE64. */
            return Convert.ToBase64String(randomBytes);
        }

        /// <summary>
        /// Sign a bearer token string using an HMAC key.
        /// </summary>
        /// <param name="hmacKey">Intiator's HMAC key encoded as BASE64.</param>
        /// <param name="bearerToken">ASCII Bearer token to sign.</param>
        /// <returns>Signature of bearer token signed using HMAC key and BASE64 encoded.</returns>
        public static string SignBearerToken(string hmacKey, string bearerToken)
        {
            /* Convert the supplied key from BASE64 to bytes. */
            IList<byte> hmacKeyBytes = Convert.FromBase64String(hmacKey);

            /* Convert the token into an array of ASCII bytes. */
            byte[] bearerTokenAsBytes = Encoding.ASCII.GetBytes(bearerToken);

            /* Sign the token. */
            using var hmac = new System.Security.Cryptography.HMACSHA256(hmacKeyBytes.ToArray());
            var signature = hmac.ComputeHash(bearerTokenAsBytes);

            /* Return the signature bytes in BASE64. */
            return Convert.ToBase64String(signature);
        }
    }
}
