/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
        /// A copy of public-draft 3.0's fixed salt in byte array form.
        /// </summary>
        private static IList<byte> VERSION_3_0_FIXED_SALT
            = Encoding.ASCII.GetBytes("BECOLRZAMVFWECYGJTLURIDPAYBGMSCQFDXTUYNPMZOAFEDGCKXTJUZLEQFCKXYB");

        /// <summary>
        /// A copy of public-draft 3.1's fixed salt in byte array form.
        /// </summary>
        private static IList<byte> VERSION_3_1_FIXED_SALT
            = new List<byte>
            {
                134, 186, 14, 196, 2, 181, 162, 234,
                156, 123, 82, 221, 66, 168, 131, 6,
                14, 181, 146, 190, 102, 141, 141, 160,
                106, 129, 196, 14, 204, 107, 217, 221
            }.AsReadOnly();

        /// <summary>
        /// Find the verification hash for this particular request.
        /// </summary>
        /// <param name="req">Loaded request object.</param>
        /// <returns>The verification hash for this object.</returns>
        public static string VerificationHash(this CallerRequest req)
        {
            /* Build JSON with each property in the expected order per RFC 8785. 
             * (Any "-UNDERDEV" suffix is removed to allow for documented hash testing.) */
            var canonical = new JObject
            {
                ["HashBack"] = req.Version.Replace("-UNDERDEV", ""),
                ["IssuerUrl"] = req.IssuerUrl,
                ["Now"] = req.Now,
                ["Rounds"] = req.Rounds,
                ["TypeOfResponse"] = req.TypeOfResponse,
                ["Unus"] = req.Unus,
                ["VerifyUrl"] = req.VerifyUrl
            };

            /* Turn it into a string of bytes with no spaces. */
            string canonicalAsString = canonical.ToString(Newtonsoft.Json.Formatting.None);
            byte[] canonicalAsBytes = UTF8.GetBytes(canonicalAsString);

            /* Select the PBKDF2 salt based on version. 
             * (Exception should never happen because CallerRequest.Parse checks.) */
            IList<byte> fixedSalt;
            if (req.Version == CallerRequest.VERSION_3_0)
                fixedSalt = VERSION_3_0_FIXED_SALT;
            else if (req.Version == CallerRequest.VERSION_3_1)
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
    }
}
