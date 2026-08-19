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

        var jwtHead = new JsonObject
        {
            ["typ"] = "JWT",
            ["alg"] = "HS256"
        };

        var jwtBody = new JsonObject
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

    internal static string? ParseAndValidateReturnSub(string cookieValue)
    {
        /* Parse JWT. If the structure is wrong then return null as an 
         * invalid cookie but unworthy of note. */
        var parts = cookieValue.Split('.');
        if (parts.Length != 3)
            return null;
        string headAndBody = parts[0] + "." + parts[1];

        /* Find the expected signature from this payload and see if it
         * matches the one provided. If not, reject it. */
        var sigAsBytes = HMACSHA256.HashData(HMACSHA256Key, Encoding.ASCII.GetBytes(headAndBody));
        string expectedSig = Helpers.JWTEncode(sigAsBytes);
        if (expectedSig != parts[2])
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
