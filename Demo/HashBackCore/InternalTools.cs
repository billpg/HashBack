/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using System;
using System.Collections.Generic;
using System.Linq;

namespace billpg.HashBackCore
{
    internal static class InternalTools
    {
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
            if ($"{url.Scheme}://{url.Authority}" != rootUrl)
                return false;
            if (url.Segments.Length != 5)
                return false;
            if (url.AbsolutePath.StartsWith("/devStoreHash/load/") == false)
                return false;

            /* Passed all tests. */
            return true;
        }

    }
}
