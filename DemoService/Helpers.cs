using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DemoService;

public static class Helpers
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
        /* Prefer CF-Connecting-Ip: Cloudflare always overwrites it with the connection it
         * actually saw, regardless of what a client sends, whereas Cloudflare only ever
         * appends to X-Forwarded-For rather than replacing it - a client can prepend any
         * IP it likes there and that value survives to this service unless something else
         * strips it first. See
         * https://developers.cloudflare.com/fundamentals/reference/http-request-headers/ */
        var cfConnectingIp = req.Headers["CF-Connecting-Ip"].FirstOrDefault();
        if (IPAddress.TryParse(cfConnectingIp?.Trim(), out var cfIp))
            return cfIp;

        /* Not behind Cloudflare (e.g. local/debug testing) - fall back to
         * X-Forwarded-For, taking the first (i.e. original client) address. */
        var xff = req.Headers["X-Forwarded-For"].FirstOrDefault();
        if (string.IsNullOrEmpty(xff))
            return IPAddress.Loopback;
        var firstIp = xff.Split(',').FirstOrDefault()?.Trim();
        if (IPAddress.TryParse(firstIp, out var ip))
            return ip;

        /* No valid IP address found, return loopback. */
        return IPAddress.Loopback;
    }



    public static bool IsIPv4(this IPAddress ip)
        => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork;
    public static bool IsIPv6(this IPAddress ip)
        => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6;
    public static bool IsIPv4(this IPNetwork net)
        => net.BaseAddress.IsIPv4();
    public static bool IsIPv6(this IPNetwork net)
        => net.BaseAddress.IsIPv6();

    public static IList<IPNetwork> Networks(IPAddress ip)
    {
        /* Start a collection of networks that will be returned. */
        var nets = new List<IPNetwork>();

        /* Convert the IP into 4 or 16 bytes. */
        var b = ip.GetAddressBytes();
        int addrLength = b.Length;
        int stepLength = 1;

        /* If IPv6, zero off the per-user 64 bits. */
        if (b.Length == 16)
        {
            for (int zeroIndex = 8; zeroIndex < 16; zeroIndex++)
                b[zeroIndex] = 0;
            addrLength = 8;
            stepLength = 2;
        }

        /* Loop through each number of bytes in the address and start setting
         * zeros for shorter prefixes. */
        for (int i = 0; i < addrLength; i += stepLength)
        {
            /* Turn the current bytes into an IP and add it to the list. */
            nets.Add(new IPNetwork(new IPAddress(b), (addrLength - i) * 8));

            /* Zero the last bytes for the next round. */
            for (int zeroOffset=0; zeroOffset<stepLength; zeroOffset++)
                b[addrLength - i - zeroOffset - 1] = 0;
        }

        /* Completed list. */
        return nets.AsReadOnly();
    }

    public static int Weight(this IPNetwork net)
    {
        /* IPv4 is simple. A /32 is heaviest while a /1 is lightest. */
        if (net.IsIPv4())
            return 5 - net.PrefixLength / 8;

        /* For IPv6, the last 64 bits have no significance for weight.
         * /64 is equal to a v4 /32. */
        if (net.IsIPv6())
            return 5 - net.PrefixLength / 16;

        /* Should never happen. */
        throw new ApplicationException("IP is neither v4 nor v6.");
    }

    public static int NetworkQuota(this IPNetwork net)
    {
        return (int)(Math.Pow(9, net.Weight()+1));
    }

    public static IDictionary<string,string> WithReplaceKeyValue(this IDictionary<string,string> old, string key, string value)
    {
        var newDict = new Dictionary<string, string>(old);
        newDict[key] = value;
        return newDict.AsReadOnly();
    }

    internal static IEnumerable<string> StringToLines(string text)
    {
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
            yield return line;
    }

    /// <summary>True for any header Cloudflare itself adds (Cf-Connecting-Ip, Cf-Ray,
    /// Cf-Warp-Tag-Id, and so on) - these describe Cloudflare's own infrastructure and the
    /// caller's connection to it, not this service, and some (like Cf-Warp-Tag-Id) are a
    /// persistent per-device identifier. Nobody outside this service has any business
    /// seeing them, so they're filtered out wherever caller headers get captured, before
    /// they're ever stored or shown in a /call report.</summary>
    internal static bool IsCloudflareHeader(string headerName)
        => headerName.StartsWith("cf-", StringComparison.OrdinalIgnoreCase);


    /// <summary>Renders "Name: Value" as two separate Markdown code spans - "`Name`: `Value`" -
    /// so a value can't visually run into its own header name. Falls back to a single code
    /// span if there's no colon to split on.</summary>
    internal static string MarkdownQuoteHeader(string header)
    {
        int colonIndex = header.IndexOf(':');
        if (colonIndex < 0)
            return MarkdownQuoteCode(header);
        return MarkdownQuoteCode(header[..colonIndex])
            + ": "
            + MarkdownQuoteCode(header[(colonIndex + 1)..].Trim());
    }

    /// <summary>
    /// Wraps arbitrary text - which may itself contain backticks, since this exists
    /// specifically for values this service doesn't control, like a target server's
    /// response headers - in a Markdown code span that actually renders as intended.
    /// Doubling every backtick in the content (an earlier version of this method) doesn't
    /// work: CommonMark closes a code span on the next run of backticks of the *same
    /// length* as the opening one, so doubled backticks either show up literally doubled in
    /// the rendered output, or - with a longer run already in the content - let the span
    /// close early and leak the rest of the content as raw, unescaped text. The correct
    /// approach is a fence longer than any run of backticks already in the content, per the
    /// CommonMark spec, with a padding space if the content starts or ends with a backtick
    /// (or is empty) so the fence can't visually merge with it.
    /// </summary>
    internal static string MarkdownQuoteCode(string code)
    {
        int longestRun = 0, currentRun = 0;
        foreach (char c in code)
        {
            currentRun = c == '`' ? currentRun + 1 : 0;
            longestRun = Math.Max(longestRun, currentRun);
        }
        string fence = new string('`', longestRun + 1);

        bool needsPadding = code.Length == 0 || code[0] == '`' || code[^1] == '`';
        return needsPadding ? $"{fence} {code} {fence}" : fence + code + fence;
    }

}
