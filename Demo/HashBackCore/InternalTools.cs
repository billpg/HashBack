/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using AddressFamily = System.Net.Sockets.AddressFamily;

namespace billpg.HashBackCore
{
    public static class InternalTools
    {
        public static T AssertNotNull<T>(this T? s) where T: class
        {
            if (s == null)
                throw new NullReferenceException("Null passed to AssertNotNull");
            return s;
        }

        /// <summary>
        /// Convert a Guid into the standard hex-with-hyphens representation.
        /// </summary>
        /// <param name="g">Guid to convert.</param>
        /// <returns>Guid in string form.</returns>
        internal static string ToHexWithHyphens(this Guid guid)
            => guid.ToString("D").ToUpperInvariant();

        /// <summary>
        /// Handy copy of the 1970 Unix Epoch.
        /// </summary>
        private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Convert a time to Unix seconds.
        /// </summary>
        /// <param name="from">DateTime instance to convert.</param>
        /// <returns>Time in seconds since the start of 1970.</returns>
        internal static long ToUnixTime(this DateTime from)
            => (long)(from.ToUniversalTime().Subtract(UNIX_EPOCH).TotalSeconds);

        public static long NowUnixTime => DateTime.UtcNow.ToUnixTime();

        public static readonly OnReadClockFn NowService = () => NowUnixTime;

        internal static bool IsClose(long x, long y, int maxDiff)
            => x > (y-maxDiff) && x < (y+maxDiff);
        
        internal static bool IsValidVerifyUrl(Uri url, string rootUrl)
        {
            /* If URL is HTTPS allow it. */
            if (url.Scheme == "https")
                return true;

            /* If URL is one of ours from the hash store service, allow it.
             * (This is only needed under debug. As a production service,
             * the hash store will use HTTPS also. */
            if (url.Root() == rootUrl &&
                url.AbsolutePath == "/hashes" &&
                url.Query.StartsWith("?ID="))
                return true;

            /* Failed all tests. */
            return false;
        }

        internal static string Root(this Uri url)
            => $"{url.Scheme}://{url.Authority}";


        /// <summary>
        /// Delegate for the NowService. 
        /// Returns a long for the number of seconds since 1970.
        /// </summary>
        /// <returns>Number of seconds since 1970.</returns>
        public delegate long OnNowFn();

        public static IssuerService.TypeOfResponse? ParseTypeOfResponse(string enumAsString)
        {
            if (enumAsString == "BearerToken")
                return IssuerService.TypeOfResponse.BearerToken;
            if (enumAsString == "JWT")
                return IssuerService.TypeOfResponse.JWT;
            if (enumAsString == "204SetCookie")
                return IssuerService.TypeOfResponse.SetCookie;
            return null;
        }

        public static string ToJsonString(this IssuerService.TypeOfResponse enumValue)
        {
            if (enumValue == IssuerService.TypeOfResponse.SetCookie)
                return "204SetCookie";
            else
                return enumValue.ToString();
        }

        public static IPAddress RemoteIP(this HttpContext context)
        {
            /* Pull out the prime remote IP. Complain if missing. */
            IPAddress? primaryRemote = context.Connection.RemoteIpAddress;
            if (primaryRemote == null)
                throw new ApplicationException("RemoteIpAddress is missing.");

            /* If the primary IP isn't localhost, return it. */
            if (primaryRemote.IsLocalhost() == false)
                return primaryRemote;

            /* Pull out the X-Forwarded-For header. If missing, return primary. */
            string? forwardedFor = context.Request.Headers["X-Forwarded-For"].SingleOrDefault();
            if (forwardedFor == null)
                return primaryRemote;

            /* Parse the header for the remote IP. If valid, return it. */
            string proxyIpAsString = forwardedFor.Split(',').Last().Trim();
            if (IPAddress.TryParse(proxyIpAsString, out var proxyIp))
                return proxyIp;

            /* Forward-For not valid. Stop everything. */
            throw new ApplicationException("X-Forwarded-For not valid.");
        }

        public static bool IsLocalhost(this IPAddress ip)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork &&
                ip.GetAddressBytes()[0] == 127)
                return true;

            if (ip.AddressFamily == AddressFamily.InterNetworkV6 &&
                ip.ToString() == IPAddress.IPv6Loopback.ToString())
                return true;

            return false;
        }
    }
}
