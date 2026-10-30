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
    /// Domain Separation Prefix used for hashing (called the "fixed salt" in
    /// earlier drafts), as specified in HashBack version 4.1.
    /// </summary>
    private static readonly byte[] DomainSeparationPrefix41 =
    [
        113,218,98,9,6,165,151,157,
        46,28,229,16,66,91,91,72,
        150,246,69,83,216,235,21,239,
        162,229,139,163,6,73,175,201
    ];

    /// <summary>
    /// Domain Separation Prefix used for hashing (called the "fixed salt" in
    /// earlier drafts), as specified in HashBack version 4.2.
    /// </summary>
    internal static readonly byte[] DomainSeparationPrefix42 =
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
            ComputeVerificationHash(DomainSeparationPrefix42, jsonAsBytes));
    }

    /// <summary>
    /// Computes the SHA-256 hash of the Domain Separation Prefix followed by the input bytes,
    /// as described in the HashBack README, and returns the result as a BASE-64 string (with trailing =).
    /// </summary>
    /// <param name="version">The HashBack version string.</param>
    /// <param name="input">The byte array to hash (typically the
    /// decoded BASE-64 Authorization payload).</param>
    /// <returns>BASE-64 encoded SHA-256 hash string.</returns>
    internal static string ComputeVerificationHash(string version, byte[] input)
    {
        /* Select the prefix based on version and call though to the other
         * function that operates on two byte arrays. */
        byte[] prefix =
            (version == VersionString41) ? DomainSeparationPrefix41 :
            (version == VersionString42) ? DomainSeparationPrefix42 :
            throw new ArgumentException("Unrecognized version string.", nameof(version));
        return ComputeVerificationHash(prefix, input);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the Domain Separation Prefix followed by the input bytes,
    /// as described in the HashBack README, and returns the result as a BASE-64 string (with trailing =).
    /// </summary>
    /// <param name="prefix">Domain Separation Prefix bytes.</param>
    /// <param name="input">Payload bytes.</param>
    /// <returns>Base-64 encoded hash string.</returns>
    internal static string ComputeVerificationHash(byte[] prefix, byte[] input)
    {
        /* Combine prefix and input. */
        byte[] prefixed = new byte[prefix.Length + input.Length];
        Buffer.BlockCopy(prefix, 0, prefixed, 0, prefix.Length);
        Buffer.BlockCopy(input, 0, prefixed, prefix.Length, input.Length);

        /* Hash with SHA-256. (SHA256.HashData is a newer static convenience method not
         * available on netstandard2.0, so this uses the older Create()/ComputeHash
         * pattern instead, which works everywhere.) */
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(prefixed);

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

    /* The default encoder escapes characters such as "+" (as \u002B) so that the JSON is
     * safe to embed in HTML, which is not a concern here and would make the Base64 block
     * differ from the JSON a reader expects to see. Relaxed escaping writes them literally. */
    private static readonly System.Text.Json.JsonSerializerOptions shortJsonOptions
        = new()
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

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
