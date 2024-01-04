using Newtonsoft.Json.Linq;
using System.Text;
using System.Security.Cryptography;

namespace billpg.CrossRequestTokenExchange
{
    /// <summary>
    /// Some helper functions assistng with the
    /// cryptography used in the token exchange.
    /// </summary>
    public static class CryptoHelpers
    {
        /// <summary>
        /// The fixed salt that will be prefixed to the JSON request body.
        /// </summary>
        public const string FIXED_SALT_AS_STRING 
            = "EAHMPQJRZDKGNVOFSIBJCZGUQAFWKDBYEGHJRUZMKFYTQPOHADJBFEXTUWLYSZNC";

        /// <summary>
        /// Copy of the fixed salt in byte form.
        /// </summary>
        public static readonly IList<byte> FIXED_SALT =
            System.Text.Encoding.ASCII.GetBytes(FIXED_SALT_AS_STRING)
            .ToList().AsReadOnly();

        /// <summary>
        /// UTF-8's GetBytes, without returning a BOM..
        /// </summary>
        private static readonly Func<string,byte[]> GetUtf8Bytes
            = new UTF8Encoding(false).GetBytes;

        /// <summary>
        /// Find the hash of the supplied JSON request body.
        /// </summary>
        /// <param name="requestBody"></param>
        /// <returns></returns>
        public static string HashRequestBody(JObject requestBody)
        {
            /* Rebuild JSON with properties in order, per RFC 8785. */
            JObject sortedRequestBody = 
                new JObject(
                    requestBody
                    .Properties()
                    .OrderBy(prop => prop.Name));

            /* Convert to string. (NewtonSoft does the right thing per RFC.) */
            string requestBodyAsString = 
                sortedRequestBody.ToString(Newtonsoft.Json.Formatting.None);

            /* Convert string to UTF-8 bytes. */
            var serializedBytes = GetUtf8Bytes(requestBodyAsString).ToList();

            /* Append the fixed salt bytes. */
            serializedBytes.AddRange(FIXED_SALT);

            /* SHA256 the completed JSON+SALT. */
            var hash = SHA256.HashData(serializedBytes.ToArray());

            /* Return as a base-64 string. */
            return Convert.ToBase64String(hash);
        }
 
        public static string GenerateUniusUsusNumerus()
        {
            /* Generate 256 cryptographic quality random bits into a block of bytes. */
            using var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[256 / 8];
            rnd.GetBytes(randomBytes);

            /* Encode those bytes as BASE64, including the trailing equals. */
            return Convert.ToBase64String(randomBytes);
        }
    }
}
