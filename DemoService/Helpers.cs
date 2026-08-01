using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DemoService;

internal static class Helpers
{
    public static byte[]? TryParseBase64(string base64)
    {
        /* Shortcut null strings. */
        if (base64 == null)
            return null;

        /* Rewrite JWT characters into normal base64. 
         * Deal with empty strings after trimming. */
        var sb = new StringBuilder(base64.Trim().TrimEnd('='));
        if (sb.Length == 0)
            return [];
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '-') sb[i] = '+';
            if (sb[i] == '_') sb[i] = '/';
        }

        /* Add padding if necessary. */
        while (sb.Length % 4 != 0)
            sb.Append('=');

        /* Finally, decode the cleaned-up string,
         * or return null if its still invalid. */
        try
        {
            return Convert.FromBase64String(sb.ToString());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public static string JWTEncode(byte[] bytes)
    {
        var sb = new StringBuilder(Convert.ToBase64String(bytes).TrimEnd('='));
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '+') sb[i] = '-';
            if (sb[i] == '/') sb[i] = '_';
        }
        return sb.ToString();
    }

    public static byte[]? TryParseBase64(string base64String, int expectedByteCount)
    {
        byte[]? bin = TryParseBase64(base64String);
        if (bin == null || bin.Length != expectedByteCount)
            return null;
        return bin;
    }

    public static IPAddress RequestIP(this HttpRequest req)
    {
        /* The TLS proxy wrapper will add an X-Forwarded-For header. Return this. */
        var xff = req.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(xff))
            return IPAddress.Loopback;

        /* Parse the first IP address in the X-Forwarded-For header. 
         * This is the original client IP. */
        var firstIp = xff.Split(',').FirstOrDefault()?.Trim();
        if (IPAddress.TryParse(firstIp, out var ip))
            return ip;

        /* No valid IP address found, return loopback. */
        return IPAddress.Loopback;
    }
}
