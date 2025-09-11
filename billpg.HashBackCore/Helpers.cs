using System;
using System.Security.Cryptography;

namespace billpg.HashBackCore
{
    public static class Helpers
    {
        /// <summary>
        /// The version string for HashBack protocol.
        /// </summary>
        public const string VersionString = "BILLPG_DRAFT_4.1";

        /// <summary>
        /// Fixed salt value used for hashing, 
        /// as specified in HashBack version 4.1.
        /// </summary>
        private static readonly byte[] FixedSalt41 =
        [
            113,218,98,9,6,165,151,157,
            46,28,229,16,66,91,91,72,
            150,246,69,83,216,235,21,239,
            162,229,139,163,6,73,175,201
        ];

        /// <summary>
        /// Computes the salted SHA-256 hash of the input bytes as described in the HashBack README,
        /// and returns the result as a BASE-64 string (with trailing =).
        /// </summary>
        /// <param name="input">The byte array to hash (typically the 
        /// decoded BASE-64 Authorization payload).</param>
        /// <returns>BASE-64 encoded salted SHA-256 hash string.</returns>
        public static string ComputeVerificationHash(byte[] input)
        {
            /* Combine salt and input. */
            byte[] salted = new byte[FixedSalt41.Length + input.Length];
            Buffer.BlockCopy(FixedSalt41, 0, salted, 0, FixedSalt41.Length);
            Buffer.BlockCopy(input, 0, salted, FixedSalt41.Length, input.Length);

            /* Hash with SHA-256. */
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(salted);

            /* Return BASE-64 string, with trailing equals. */
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Computes the salted SHA-256 hash of the input BASE-64 string 
        /// as described in the HashBack README, and returns the result 
        /// as a BASE-64 string.
        /// </summary>
        /// <param name="base64Input">The BASE-64 encoded string to decode and hash.</param>
        /// <returns>BASE-64 encoded salted SHA-256 hash string.</returns>
        public static string ComputeVerificationHash(string base64Input)
            => ComputeVerificationHash(Convert.FromBase64String(base64Input));

        /// <summary>
        /// Converts a DateTime to Unix time seconds, converting to UTC if necessary.
        /// Throws if DateTime.Kind is Unspecified.
        /// </summary>
        /// <param name="dt">The DateTime to convert (must be Utc or Local).</param>
        /// <returns>Seconds since 1970-01-01T00:00:00Z.</returns>
        /// <exception cref="ArgumentException">Thrown if DateTime.Kind is Unspecified.</exception>
        public static long ToUnixTimeSeconds(this DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
                throw new ArgumentException("DateTime.Kind must not be Unspecified. Use Utc or Local.", nameof(dt));
            if (dt.Kind == DateTimeKind.Local)
                dt = dt.ToUniversalTime();
            return (long)(dt - DateTime.UnixEpoch).TotalSeconds;
        }

        internal static bool EqualsNoCase(string x, string y)
            => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);
    }

    public class AuthorizationParseException : Exception
    {
        public AuthorizationParseException(string message) : base(message) { }
    }

    public readonly struct AuthorizationParseResult
    {
        public string VerifyUrl { get; }
        public string ExpectedHash { get; }

        public AuthorizationParseResult(string verifyUrl, string expectedHash)
        {
            VerifyUrl = verifyUrl;
            ExpectedHash = expectedHash;
        }
    }

}
