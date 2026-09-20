using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

[assembly:InternalsVisibleTo("UpdateReadme")]

namespace billpg.HashBackCore;

internal static class Helpers
{
    // HashBack version strings.
    internal const string VersionString41 = "BILLPG_DRAFT_4.1";
    internal const string VersionString42 = "BILLPG_DRAFT_4.2";

    /// <summary>DateTime.UnixEpoch isn't available on netstandard2.0, so this is the
    /// equivalent used everywhere in this library instead.</summary>
    internal static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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

        /* Hash with SHA-256. (SHA256.HashData is a newer static convenience method not
         * available on netstandard2.0, so this uses the older Create()/ComputeHash
         * pattern instead, which works everywhere.) */
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(salted);

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
        return (long)(dt - UnixEpoch).TotalSeconds;
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
        /* Generate 16 cryptographic-quality random bytes and encode as BASE-64.
         * (RandomNumberGenerator.Fill is a newer static method not available on
         * netstandard2.0, so this uses the older Create()/GetBytes pattern instead.) */
        byte[] unusBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(unusBytes);
        return Convert.ToBase64String(unusBytes);
    }
}

/// <summary>The specific reason a HashBack request was rejected, alongside the free-text
/// <see cref="Exception.Message"/> on the <see cref="AuthorizationParseException"/> that carries it.</summary>
public enum ValidateRejectionReason
{
    /// <summary>The header itself is malformed - not valid base-64/JSON, or missing a required property.</summary>
    BadHeader,

    /// <summary>The Host value is not one this policy accepts. See <see cref="HashBackPolicy.OnHostValidate"/>.</summary>
    WrongHost,

    /// <summary>The Now value is outside this policy's allowed window. See <see cref="HashBackPolicy.OnNowValidate"/>.</summary>
    WrongNow,

    /// <summary>The Unus value has already been used. See <see cref="HashBackPolicyExtensions.RequireUnusNotReused"/>.</summary>
    ReplayedUnus,

    /// <summary>The Verify URL doesn't correspond to a known user. See <see cref="HashBackPolicy.OnIdentifyUser"/>.</summary>
    UnknownUser,

    /// <summary>The downloaded verification hash didn't match the hash this request actually computes.</summary>
    WrongHash
}

/// <summary>Thrown when a HashBack Authorization header is malformed, or is rejected by the
/// policy it's authenticated against. See <see cref="Reason"/> for the specific cause.</summary>
public class AuthorizationParseException : Exception
{
    /// <summary>The specific reason this request was rejected.</summary>
    public ValidateRejectionReason Reason { get; }

    /// <summary>Constructs a new AuthorizationParseException with the given message and reason.</summary>
    public AuthorizationParseException(string message, ValidateRejectionReason reason)
        : base(message)
    {
        this.Reason = reason;
    }
}
