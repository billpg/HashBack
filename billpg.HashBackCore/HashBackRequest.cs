using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace billpg.HashBackCore;

/// <summary>
/// Represents a single HashBack request - either freshly built ready for sending as a
/// client, or parsed from an incoming Authorization header ready for authentication as
/// a server.
/// </summary>
public class HashBackRequest
{
    public string Version { get; }
    public string Host { get; }
    public long Now { get; }
    public string Unus { get; }
    public Uri Verify { get; }
    public IReadOnlyList<byte> JsonAsBytes { get; }

    /// <summary>The BASE-64 encoded JSON block, ready for use in an "Authorization: HashBack" header.</summary>
    public string AuthToken
        => Convert.ToBase64String(JsonAsBytes.ToArray());

    /// <summary>The verification hash expected to be published at the Verify URL.</summary>
    public string VerificationHash
        => Helpers.ComputeVerificationHash(Version, JsonAsBytes.ToArray());

    /// <summary>The Now property, converted to a UTC DateTime.</summary>
    public DateTime NowAsDateTime
        => DateTime.UnixEpoch.AddSeconds(Now);

    private HashBackRequest(string version, string host, long now, string unus, Uri verify, byte[] jsonAsBytes)
    {
        this.Version = version;
        this.Host = host;
        this.Now = now;
        this.Unus = unus;
        this.Verify = verify;
        this.JsonAsBytes = jsonAsBytes.ToList().AsReadOnly();
    }

    /// <summary>
    /// Parses an incoming Authorization header value into a HashBackRequest object. This
    /// checks the header is well-formed - including that the Verify URL is secure - but
    /// applies none of a server's own policy. Call Authenticate afterwards to apply that.
    /// </summary>
    /// <param name="authHeader">The raw Authorization header value, with or without the leading "HashBack " scheme name.</param>
    /// <exception cref="AuthorizationParseException">Thrown if the header is malformed.</exception>
    public static HashBackRequest Parse(string authHeader)
    {
        /* If the header includes the authentication scheme name, cut it. */
        const string authPrefix = "HashBack ";
        if (authHeader.StartsWith(authPrefix, StringComparison.OrdinalIgnoreCase))
            authHeader = authHeader.Substring(authPrefix.Length).Trim();
        else
            authHeader = authHeader.Trim();

        /* Attempt to decode from base-64. */
        byte[]? jsonAsBytes = Helpers.TryBase64Decode(authHeader);
        string json;
        if (jsonAsBytes != null)
        {
            /* Successfully decoded base-64. Convert to string. */
            json = Encoding.UTF8.GetString(jsonAsBytes);
        }
        /* Could this be an unencoded JSON string instead? */
        else if (authHeader.StartsWith('{') && authHeader.EndsWith('}'))
        {
            /* Use it directly. The JSON-Validate farther down will reject if not.
             * (We will still need bytes for hashing later so save those.) */
            json = authHeader;
            jsonAsBytes = Encoding.UTF8.GetBytes(authHeader);
        }
        /* Complain that it's neither valid base-64 nor JSON. */
        else
        {
            throw new AuthorizationParseException(
                "Authorization header is not valid BASE-64.",
                ValidateRejectionReason.BadHeader);
        }

        /* Attempt to parse JSON, complaining if it rejects the string. */
        JsonObject obj = Helpers.TryJsonParse(json)
            ?? throw new AuthorizationParseException(
                "Authorization header is not valid JSON.",
                ValidateRejectionReason.BadHeader);

        /* Extract and validate Version. */
        var version = obj[nameof(Version)]?.GetValue<string>()
            ?? throw new AuthorizationParseException(
                "Version property is missing.",
                ValidateRejectionReason.BadHeader);
        if (!Helpers.IsRecognizedVersion(version))
            throw new AuthorizationParseException(
                $"Version must be one of {Helpers.SupportedVersions.Select(v => $"'{v}'").ToSeparatedString("/")}.",
                ValidateRejectionReason.BadHeader);

        /* Extract Host. (Validation of the value is left to the policy.) */
        var host = obj[nameof(Host)]?.GetValue<string>()
            ?? throw new AuthorizationParseException(
                "Host property is missing.", ValidateRejectionReason.BadHeader);

        /* Extract Now. (Validation of the value is left to the policy.) */
        var now = obj[nameof(Now)]?.GetValue<long?>()
            ?? throw new AuthorizationParseException(
                "Now property is missing.",
                ValidateRejectionReason.BadHeader);

        /* Extract and validate Unus is well-formed. (Replay checks are left to the policy.) */
        var unus = obj[nameof(Unus)]?.GetValue<string>()
            ?? throw new AuthorizationParseException(
                "Unus property is missing.", ValidateRejectionReason.BadHeader);
        var unusAsBytes = Helpers.TryBase64Decode(unus)
            ?? throw new AuthorizationParseException(
                "Unus property is not base-64.", ValidateRejectionReason.BadHeader);
        if (unusAsBytes.Length < 128 / 8)
            throw new AuthorizationParseException(
                "Unus property is not 128 bits.", ValidateRejectionReason.BadHeader);

        /* Validate Verify as a secure URL. (Mapping it to a user is left to the policy.) */
        var verifyAsString = obj[nameof(Verify)]?.GetValue<string>()
            ?? throw new AuthorizationParseException(
                "Verify property is missing.",
                ValidateRejectionReason.BadHeader);
        if (!Uri.TryCreate(verifyAsString, UriKind.Absolute, out var verifyUrl))
            throw new AuthorizationParseException(
                "Verify property must be a valid URL.",
                ValidateRejectionReason.BadHeader);
        if (!IsSecureVerifyUrl(verifyUrl))
            throw new AuthorizationParseException(
                "Verify URL must be HTTPS.",
                ValidateRejectionReason.BadHeader);

        /* Return completed object. */
        return new HashBackRequest(version, host, now, unus, verifyUrl, jsonAsBytes);
    }

    /// <summary>
    /// The Verify URL must use HTTPS, except for the special case of "http://localhost"
    /// which is allowed to ease local development and testing.
    /// </summary>
    private static bool IsSecureVerifyUrl(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps
        || (uri.Scheme == Uri.UriSchemeHttp && uri.Host == "localhost");

    /// <summary>
    /// Authenticates this request against the supplied policy: checks the Host, Now and
    /// Unus properties, identifies the calling user from the Verify URL, downloads the
    /// verification hash published there, and confirms it matches this request.
    /// </summary>
    /// <returns>The user identity string returned by the policy's OnIdentifyUser handler.</returns>
    /// <exception cref="AuthorizationParseException">Thrown if any check is rejected by the policy.</exception>
    public async Task<string> Authenticate(HashBackPolicy policy)
    {
        /* Validate Host. */
        policy.OnLogWrite($"OnHostValidate(\"{Host}\")");
        if (!await policy.OnHostValidate(Host))
            throw new AuthorizationParseException(
                "Host property is not valid for this server.",
                ValidateRejectionReason.WrongHost);

        /* Validate Now. */
        policy.OnLogWrite($"OnNowValidate({Now})");
        if (!await policy.OnNowValidate(Now))
            throw new AuthorizationParseException(
                "Now property is not valid for this server's time policy.",
                ValidateRejectionReason.WrongNow);

        /* Validate Unus, typically for replay protection. */
        policy.OnLogWrite($"OnUnusValidate(\"{Unus}\")");
        if (!await policy.OnUnusValidate(Unus))
            throw new AuthorizationParseException(
                "Unus property has already been used.",
                ValidateRejectionReason.ReplayedUnus);

        /* Identify the user this verification URL corresponds to. */
        policy.OnLogWrite($"OnIdentifyUser(\"{Verify}\")");
        string? user = await policy.OnIdentifyUser(Verify);
        if (user == null)
            throw new AuthorizationParseException(
                $"Verify=\"{Verify}\" does not correspond to a known user.",
                ValidateRejectionReason.UnknownUser);
        policy.OnLogWrite($"OnIdentifyUser returned \"{user}\".");

        /* Download the verification hash and compare against the expected value. */
        string expectedHash = VerificationHash;
        policy.OnLogWrite($"OnGetVerificationHash(\"{Verify}\")");
        string actualHash = await policy.OnGetVerificationHash(Verify);
        policy.OnLogWrite($"OnGetVerificationHash returned \"{actualHash}\".");
        if (actualHash != expectedHash)
            throw new AuthorizationParseException(
                "The verification hash did not match the expected hash.",
                ValidateRejectionReason.WrongHash);

        /* All checks passed. */
        policy.OnLogWrite($"Authenticated as \"{user}\".");
        return user;
    }

    /// <summary>Parses the supplied header and immediately authenticates it against the supplied policy.</summary>
    public static async Task<string> Authenticate(string authHeader, HashBackPolicy policy)
        => await Parse(authHeader).Authenticate(policy);

    /// <summary>Builds a new request for right now, with a fresh cryptographic-quality random Unus value.</summary>
    public static HashBackRequest Create(string host, Uri verify)
        => Create(host, DateTime.UtcNow, Helpers.GenerateUnus(), verify);

    /// <summary>Builds a new request for the supplied time, with a fresh cryptographic-quality random Unus value.</summary>
    public static HashBackRequest Create(string host, DateTime now, Uri verify)
        => Create(host, now, Helpers.GenerateUnus(), verify);

    public static HashBackRequest Create(string host, DateTime now, string unus, Uri verify)
        => Create(host, now.ToUnixTimeSeconds(), unus, verify);

    public static HashBackRequest Create(string host, long now, string unus, Uri verify)
        => Create(Helpers.VersionString42, host, now, unus, verify);

    /// <summary>Builds a new request using a specific version string. Intended for testing older draft versions.</summary>
    internal static HashBackRequest Create(string version, string host, long now, string unus, Uri verify)
        => new(version, host, now, unus, verify,
            new JsonObject
            {
                [nameof(Version)] = version,
                [nameof(Host)] = host,
                [nameof(Now)] = now,
                [nameof(Unus)] = unus,
                [nameof(Verify)] = verify.ToString()
            }.ToShortJson().ToUtf8());
}
