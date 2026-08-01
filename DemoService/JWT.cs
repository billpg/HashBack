using Microsoft.AspNetCore.Http.Features;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DemoService;

internal static class JWT
{
    /// <summary>
    /// JWT key that's good for this run of the demo service only.
    /// </summary>
    private static readonly byte[] HMACSHA256Key = RandomBytes(256/8);

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

        var jwtHead = new JObject
        {
            ["typ"] = "JWT",
            ["alg"] = "HS256"
        };

        var jwtBody = new JObject
        {
            ["sub"] = sub,
            ["iat"] = nowUnix,
            ["exp"] = nowUnix + 60 * 30,
            ["bonus"] = "https://youtu.be/XfELJU1mRMg"
        };

        string headAndBody =
            JWTEncode(jwtHead) + "." + JWTEncode(jwtBody);
        var sigAsBytes = HMACSHA256.HashData(HMACSHA256Key, Encoding.ASCII.GetBytes(headAndBody));

        return headAndBody + "." + Helpers.JWTEncode(sigAsBytes);
    }

    internal static string ParseAndValidateReturnSub(string cookieValue)
    {
        var parts = cookieValue.Split('.');
        if (parts.Length != 3)
            throw new InvalidOperationException("Invalid JWT format.");
        string headAndBody = parts[0] + "." + parts[1];
        var sigAsBytes = HMACSHA256.HashData(HMACSHA256Key, Encoding.ASCII.GetBytes(headAndBody));
        string expectedSig = Helpers.JWTEncode(sigAsBytes);
        if (!string.Equals(expectedSig, parts[2], StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid JWT signature.");
        var bodyJson = JWTDecode(parts[1]);
        long expUnix = bodyJson.Value<long>("exp");
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (nowUnix > expUnix)
            throw new InvalidOperationException("JWT has expired.");
        return bodyJson.Value<string>("sub") ?? throw new InvalidOperationException("JWT missing 'sub' claim.");
    }

    private static string JWTEncode(JObject json)
    {
        var jsonAsString = json.ToString(Newtonsoft.Json.Formatting.None);
        var jsonAsBytes = Encoding.UTF8.GetBytes(jsonAsString);
        return Helpers.JWTEncode(jsonAsBytes);
    }

    private static JObject JWTDecode(string base64Url)
    {
        var bytes = Helpers.TryParseBase64(base64Url) 
            ?? throw new InvalidOperationException("Invalid base64url encoding.");
        var jsonAsString = Encoding.UTF8.GetString(bytes);
        return JObject.Parse(jsonAsString);
    }
}
