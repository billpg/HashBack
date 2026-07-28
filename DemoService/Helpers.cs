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
    public static byte[]? TryParseBase64(string base64String)
    {
        try
        {
            return Convert.FromBase64String(base64String.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
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
