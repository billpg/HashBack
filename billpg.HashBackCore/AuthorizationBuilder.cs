using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    public readonly struct AuthorizationBuildResult
    {
        public string AuthHeader { get; }
        public string VerificationHash { get; }

        public AuthorizationBuildResult(string authHeader, string verificationHash)
        {
            AuthHeader = authHeader;
            VerificationHash = verificationHash;
        }
    }

    public static class AuthorizationBuilder
    {
        /// <summary>
        /// Produces the BASE-64 block suitable for use in a HashBack Authorization header,
        /// and the expected verification hash string.
        /// </summary>
        /// <param name="host">The full domain name of the server being called.</param>
        /// <param name="now">The current UTC time, as seconds since 1970-01-01.</param>
        /// <param name="unus">A 128-bit cryptographic-quality random value in BASE-64.</param>
        /// <param name="verify">The HTTPS URL where the verification hash will be published.</param>
        /// <returns>
        /// An <see cref="AuthorizationBuildResult"/> containing the BASE-64 encoded JSON block and the verification hash.
        /// </returns>
        public static AuthorizationBuildResult BuildAuthorization(
            string host,
            long now,
            string unus,
            string verify)
        {
            /* Serialize parameters to JSON and encode. */
            string json = new JObject
            {
                ["Version"] = Helpers.VersionString,
                ["Host"] = host,
                ["Now"] = now,
                ["Unus"] = unus,
                ["Verify"] = verify
            }.ToString(Newtonsoft.Json.Formatting.None);
            byte[] jsonAsBytes = Encoding.UTF8.GetBytes(json);
            string authHeader = Convert.ToBase64String(jsonAsBytes);

            /* Compute the verification hash using the Helpers function and return. */
            string verificationHash = Helpers.ComputeVerificationHash(jsonAsBytes);
            return new AuthorizationBuildResult(authHeader, verificationHash);
        }

        /// <summary>
        /// Produces the BASE-64 block and verification hash for a HashBack Authorization header,
        /// generating its own Now and Unus values.
        /// </summary>
        /// <param name="host">The full domain name of the server being called.</param>
        /// <param name="verify">The HTTPS URL where the verification hash will be published.</param>
        /// <returns>
        /// An <see cref="AuthorizationBuildResult"/> containing the BASE-64 encoded JSON block and the verification hash.
        /// </returns>
        public static AuthorizationBuildResult BuildAuthorization(
            string host,
            string verify)
        {
            /* Now: current UTC time in seconds since 1970-01-01. */
            long now = DateTime.UtcNow.ToUnixTimeSeconds();

            /* Unus: 128 bits (16 bytes) of cryptographic-quality
             * random data, BASE-64 encoded. */
            byte[] unusBytes = new byte[16];
            RandomNumberGenerator.Fill(unusBytes);
            string unus = Convert.ToBase64String(unusBytes);

            /* Call through to the main function. */
            return BuildAuthorization(host, now, unus, verify);
        }
    }
}
