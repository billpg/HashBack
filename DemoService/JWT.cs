using DemoService.Services;
using Microsoft.AspNetCore.Http.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DemoService;

internal static class JWT
{
    private static byte[]? explicitKey;

    /// <summary>Thread-safe fallback for tests (run with MSTest's method-level
    /// parallelism) that never call Initialize - Lazy&lt;T&gt; guarantees the factory runs
    /// exactly once even if multiple tests race to touch HMACSHA256Key simultaneously,
    /// unlike a plain "field ??= GenerateRandomKey()" which isn't atomic. The original
    /// "static readonly" field this replaced got that guarantee for free from the CLR's own
    /// type initializer; losing it silently was a real, if narrow, bug.</summary>
    private static readonly Lazy<byte[]> fallbackKey = new(GenerateRandomKey);

    /// <summary>
    /// Sets the signing key used for every JWT created or validated for the rest of this
    /// process's lifetime. Call once at startup, before any request arrives, with a key
    /// loaded from (or freshly generated and saved to) the database, so cookies issued
    /// before a restart stay valid afterward. If this is never called - as in most unit
    /// tests - a fresh random key is generated on first use instead (see fallbackKey),
    /// which is fine for the lifetime of a single test run but is never persisted anywhere.
    /// </summary>
    internal static void Initialize(byte[] key) => explicitKey = key;

    private static byte[] HMACSHA256Key => explicitKey ?? fallbackKey.Value;

    /// <summary>Generates a fresh, cryptographically random 256-bit key suitable for
    /// Initialize - exposed so Program.cs can create one the first time this service ever
    /// starts, without duplicating the RNG logic here.</summary>
    internal static byte[] GenerateRandomKey() => RandomBytes(256 / 8);

    /// <summary>
    /// How long a token remains valid after being issued. The Set-Cookie header's own
    /// Expires attribute should always match this exactly, so the cookie's browser-side
    /// lifetime and its actual enforced validity never disagree.
    /// </summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }

    internal static string Create(string sub)
    {
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var jwtHead = new JsonObject
        {
            ["typ"] = "JWT",
            ["alg"] = "HS256"
        };

        var jwtBody = new JsonObject
        {
            ["sub"] = sub,
            ["iat"] = nowUnix,
            ["exp"] = nowUnix + (long)Lifetime.TotalSeconds,
            ["bonus"] = "https://youtu.be/XfELJU1mRMg"
        };

        string headAndBody =
            JWTEncode(jwtHead) + "." + JWTEncode(jwtBody);
        var sigAsBytes = HMACSHA256.HashData(HMACSHA256Key, Encoding.ASCII.GetBytes(headAndBody));

        return headAndBody + "." + Helpers.JWTEncode(sigAsBytes);
    }

    internal static string? ParseAndValidateReturnSub(string cookieValue)
    {
        /* Parse JWT. If the structure is wrong then return null as an 
         * invalid cookie but unworthy of note. */
        var parts = cookieValue.Split('.');
        if (parts.Length != 3)
            return null;
        string headAndBody = parts[0] + "." + parts[1];

        /* Find the expected signature from this payload and see if it matches the one
         * provided, comparing the raw bytes in constant time rather than the encoded
         * strings - an ordinary string comparison short-circuits on the first mismatched
         * character, which is a textbook timing side-channel for a MAC check. */
        var sigAsBytes = HMACSHA256.HashData(HMACSHA256Key, Encoding.ASCII.GetBytes(headAndBody));
        var providedSigAsBytes = Helpers.TryParseBase64(parts[2]);
        if (providedSigAsBytes == null || !CryptographicOperations.FixedTimeEquals(sigAsBytes, providedSigAsBytes))
            return null;

        /* From this point, any issues with signed cookies are problems worthy of an exception. */
        var bodyJson = JWTDecode(parts[1]);
        long expUnix = bodyJson["exp"]!.GetValue<long>();
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowUnix > expUnix)
            return null;
        return bodyJson["sub"]!.GetValue<string>() ?? throw new InvalidOperationException("JWT missing 'sub' claim.");
    }

    private static string JWTEncode(JsonObject json)
    {
        var jsonAsString = json.ToString();
        var jsonAsBytes = Encoding.UTF8.GetBytes(jsonAsString);
        return Helpers.JWTEncode(jsonAsBytes);
    }

    private static JsonObject JWTDecode(string base64Url)
    {
        var bytes = Helpers.TryParseBase64(base64Url) 
            ?? throw new InvalidOperationException("Invalid base64url encoding.");
        var jsonAsString = Encoding.UTF8.GetString(bytes);
        return (JsonObject)JsonNode.Parse(jsonAsString)!;
    }
}
