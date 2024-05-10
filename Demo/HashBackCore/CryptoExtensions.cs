/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Collection of cryptographic helpers.
    /// </summary>
    public static class CryptoExtensions
    {
        /// <summary>
        /// Handy copy of UTF8 encoding with no-BOM flagged.
        /// </summary>
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

        /// <summary>
        /// Compute SHA256 hash from bytes.
        /// </summary>
        private static readonly Func<byte[], byte[]> ComputeSha256
            = System.Security.Cryptography.SHA256.Create().ComputeHash;

        public static readonly IList<string> ValidVersions 
            = new List<string> { IssuerService.VERSION_3_0, IssuerService.VERSION_3_1 }
            .AsReadOnly();

        /// <summary>
        /// A copy of public-draft 3.0's fixed salt in byte array form.
        /// </summary>
        private static readonly IList<byte> VERSION_3_0_FIXED_SALT
            = Encoding.ASCII.GetBytes("BECOLRZAMVFWECYGJTLURIDPAYBGMSCQFDXTUYNPMZOAFEDGCKXTJUZLEQFCKXYB")
            .AsReadOnly();

        /// <summary>
        /// A copy of public-draft 3.1's fixed salt in byte array form.
        /// </summary>
        private static readonly IList<byte> VERSION_3_1_FIXED_SALT
            = new List<byte>
            {
                113, 218, 98, 9, 6, 165, 151, 157,
                46, 28, 229, 16, 66, 91, 91, 72,
                150, 246, 69, 83, 216, 235, 21, 239,
                162, 229, 139, 163, 6, 73, 175, 201
            }.AsReadOnly();

        /// <summary>
        /// Find the verification hash for this particular request.
        /// </summary>
        /// <param name="req">Loaded request object.</param>
        /// <returns>The verification hash for this object.</returns>
        public static string VerificationHash(this IssuerService.Request req)
        {
            /* Build JSON with each property in the expected order per RFC 8785. */
            var canonical = new JObject
            {
                ["HashBack"] = req.HashBack,
                ["IssuerUrl"] = req.IssuerUrl,
                ["Now"] = req.Now,
                ["Rounds"] = req.Rounds,
                ["TypeOfResponse"] = req.TypeOfResponse,
                ["Unus"] = req.Unus,
                ["VerifyUrl"] = req.VerifyUrl
            };

            /* Turn it into a string of bytes with no spaces. */
            string canonicalAsString = canonical.ToStringOneLine();
            byte[] canonicalAsBytes = UTF8.GetBytes(canonicalAsString);

            /* Select the PBKDF2 salt based on version. 
             * (Exception should never happen because CallerRequest.Parse checks.) */
            IList<byte> fixedSalt;
            if (req.HashBack == IssuerService.VERSION_3_0)
                fixedSalt = VERSION_3_0_FIXED_SALT;
            else if (req.HashBack == IssuerService.VERSION_3_1)
                fixedSalt = VERSION_3_1_FIXED_SALT;
            else
                throw new ApplicationException("Unknown version.");

            /* Run PBKDF2 as documented. (3.0 and 3.1 only differ on salt selection.) */
            byte[] hashAsBytes = Rfc2898DeriveBytes.Pbkdf2(
                password: canonicalAsBytes,
                salt: fixedSalt.ToArray(),
                hashAlgorithm: HashAlgorithmName.SHA256,
                iterations: req.Rounds,
                outputLength: 256 / 8);

            /* Return hash in BASE-64. */
            return Convert.ToBase64String(hashAsBytes);
        }

        public static string ExpectedHashFourDotZero(IList<byte> authHeaderAsBytes, int rounds)
        {
            /* Perform the PBKDF2 argorithm as required by draft spec 4.0.
             * (Note the fixed salt is made up of the same bytes as with 3.1) */
            byte[] hashAsBytes = Rfc2898DeriveBytes.Pbkdf2(
                password: authHeaderAsBytes.ToArray(),
                salt: VERSION_3_1_FIXED_SALT.ToArray(),
                hashAlgorithm: HashAlgorithmName.SHA256,
                iterations: rounds,
                outputLength: 256 / 8);

            /* Return hash in BASE-64. */
            return Convert.ToBase64String(hashAsBytes);
        }

        /// <summary>
        /// Generate a string token that could be used as a Unus property value.
        /// </summary>
        /// <returns>256 bits of cryptgraphic quality randomness in base-64 encoding.</returns>
        public static string GenerateUnus()
        {
            /* Generate 256 cryptographic quality random bits into a block of bytes. */
            using var rnd = RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[256 / 8];
            rnd.GetBytes(randomBytes);

            /* Encode those bytes as BASE64, including the trailing equals. */
            return Convert.ToBase64String(randomBytes);
        }

        public static string UserToHashedUser(string user, string secret)
        {
            /* Call PBKDF2 to turn the name and salt into a key. */
            byte[] hashAsBytes = Rfc2898DeriveBytes.Pbkdf2(
                password: UTF8.GetBytes(user),
                salt: UTF8.GetBytes(secret),
                hashAlgorithm: HashAlgorithmName.SHA512,
                iterations: 3,
                outputLength: 99);

            /* Encode, removing and unwanted characters. */
            string hashAsString = 
                Convert.ToBase64String(hashAsBytes)
                .Replace("/", "")
                .Replace("+", "")
                .Replace("=", "");

            /* Return the left-most ten characters. */
            return hashAsString.Substring(0, 10);            
        }

        internal static IList<byte> RandomBytes(int byteCount)
        {
            using var rnd = RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[byteCount];
            rnd.GetBytes(randomBytes);
            return randomBytes;
        }
    }
}
