using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

[assembly:InternalsVisibleTo("UpdateReadme")]

namespace billpg.HashBackCore
{
    internal static class Helpers
    {
        // HashBack version strings.
        internal const string VersionString41 = "BILLPG_DRAFT_4.1";
        internal const string VersionString42 = "BILLPG_DRAFT_4.2";

        /// <summary>
        /// Returns an immutable list of supported HashBack version strings.
        /// </summary>
        public static IEnumerable<string> SupportedVersions 
            => new List<string> { VersionString41, VersionString42 }.AsReadOnly();

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
        /// Fixed salt value used for hashing,
        /// as specified in HashBack version 4.2.
        /// </summary>
        internal static readonly byte[] FixedSalt42 =
        [
            48,106,239,61,141,188,122,117,
            71,242,89,164,154,89,44,47,
            20,42,34,245,250,230,139,30,
            56,240,40,168,35,184,92,252
        ];

        /// <summary>
        /// Validates if a version string is recognized by 
        /// the currently running HashBackCore library.
        /// </summary>
        /// <param name="version">Version string that may have been supplied in an Authorization: header.</param>
        /// <returns>True if this version of the HashBackCore library knows about this version.</returns>
        public static bool IsRecognizedVersion(string version)
            => SupportedVersions.Contains(version);

        /// <summary>
        /// Builds a HashBack Authorization header value and its corresponding
        /// verification hash, using draft version 4.2 format.
        /// </summary>
        /// <param name="host">Host property.</param>
        /// <param name="now">Now property.</param>
        /// <param name="unus">Unus property.</param>
        /// <param name="verify">Verify property.</param>
        /// <returns>Authenticate header and verification hash.</returns>
        public static (string authHeader, string verificationHash) Build(string host, long now, string unus, string verify)
        {
            /* Serialize parameters to JSON and encode. */
            string json = new JsonObject
            {
                ["Version"] = VersionString42,
                ["Host"] = host,
                ["Now"] = now,
                ["Unus"] = unus,
                ["Verify"] = verify
            }.ToShortJson();
            byte[] jsonAsBytes = Encoding.UTF8.GetBytes(json);

            /* Compute the verification hash from the above byte array and return. */
            return (
                Convert.ToBase64String(jsonAsBytes), 
                ComputeVerificationHash(FixedSalt42, jsonAsBytes));
        }

        /// <summary>
        /// Computes the salted SHA-256 hash of the input bytes as described in the HashBack README,
        /// and returns the result as a BASE-64 string (with trailing =).
        /// </summary>
        /// <param name="version">The HashBack version string.</param>
        /// <param name="input">The byte array to hash (typically the 
        /// decoded BASE-64 Authorization payload).</param>
        /// <returns>BASE-64 encoded salted SHA-256 hash string.</returns>
        internal static string ComputeVerificationHash(string version, byte[] input)
        {
            /* Select salt based on version and call though to the other
             * function that operates on two byte arrays. */
            byte[] salt = 
                (version == VersionString41) ? FixedSalt41 : 
                (version == VersionString42) ? FixedSalt42 :
                throw new ArgumentException("Unrecognized version string.", nameof(version));
            return ComputeVerificationHash(salt, input);
        }

        /// <summary>
        /// Computes the salted SHA-256 hash of the input bytes as described in the HashBack README,
        /// and returns the result as a BASE-64 string (with trailing =).
        /// </summary>
        /// <param name="salt">Salt bytes.</param>
        /// <param name="input">Payload bytes.</param>
        /// <returns>Base-64 encoded salted hash string.</returns>
        internal static string ComputeVerificationHash(byte[] salt, byte[] input)
        {
            /* Combine salt and input. */
            byte[] salted = new byte[salt.Length + input.Length];
            Buffer.BlockCopy(salt, 0, salted, 0, salt.Length);
            Buffer.BlockCopy(input, 0, salted, salt.Length, input.Length);

            /* Hash with SHA-256. */
            byte[] hash = SHA256.HashData(salted);

            /* Return BASE-64 string, with trailing equals. */
            return Convert.ToBase64String(hash);
        }

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

        internal static byte[]? TryBase64Decode(string base64)
        {
            try
            {
                return Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return null!;
            }
        }

        internal static JsonObject? TryJsonParse(string json)
        {
            try
            {
                return (JsonObject)JsonNode.Parse(json)!;
            }
            catch (System.Text.Json.JsonException)
            {
                return null!;
            }
        }

        private static readonly System.Text.Json.JsonSerializerOptions shortJsonOptions
            = new() { WriteIndented = false };

        public static string ToShortJson(this JsonObject j)
            => j.ToJsonString(shortJsonOptions);
        
        internal static byte[] ToUtf8(this string s)
            => Encoding.UTF8.GetBytes(s);

        /// <summary>
        /// DateTime.UtcNow wrapped in a function, 
        /// so it can be passed as a delegate.
        /// </summary>
        /// <returns>Result of calling DateTime.UtcNow 
        /// at the point it is called.</returns>
        internal static DateTime DateTimeUtcNowAsDelegate()
            => DateTime.UtcNow;

        internal static void DefaultLogWrite(string logText)
        {
            /* Nothing to do. */
        }

        /// <summary>
        /// Concatenates the elements of a sequence, using the specified separator between each element.
        /// </summary>
        /// <param name="items">The sequence of strings to concatenate. If <paramref name="items"/> is empty, the result is an empty string.</param>
        /// <param name="separator">The string to use as a separator. If <paramref name="separator"/> is <see langword="null"/>, an empty string
        /// is used instead.</param>
        /// <returns>A single string that consists of the elements in <paramref name="items"/> delimited by the <paramref
        /// name="separator"/> string.</returns>
        internal static string ToSeparatedString(this IEnumerable<string> items, string separator)
            => string.Join(separator, items);

        internal static string GenerateUnus()
        {
            /* Generate 16 cryptographic-quality 
             * random bytes and encode as BASE-64. */
            byte[] unusBytes = new byte[16];
            RandomNumberGenerator.Fill(unusBytes);
            return Convert.ToBase64String(unusBytes);
        }
    }

    public enum ValidateRejectionReason
    {
        BadHeader,
        WrongHost,
        WrongNow,
        ReplayedUnus,
        UnknownUser,
        WrongHash
    }

    public class AuthorizationParseException : Exception
    {
        public ValidateRejectionReason Reason { get; }

        public AuthorizationParseException(string message, ValidateRejectionReason reason) 
            : base(message) 
        {
            this.Reason = reason;
        }
    }
}
