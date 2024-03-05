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

        /// <summary>
        /// Generate a string token that could be used as a Unus property value.
        /// </summary>
        /// <returns>256 bits of cryptgraphic quality randomness in base-64 encoding.</returns>
        internal static string GenerateUnus()
        {
            /* Generate 256 cryptographic quality random bits into a block of bytes. */
            using var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[256 / 8];
            rnd.GetBytes(randomBytes);

            /* Encode those bytes as BASE64, including the trailing equals. */
            return Convert.ToBase64String(randomBytes);
        }

        internal static bool IsClose(long x, long y, int maxDiff)
            => x > (y-maxDiff) && x < (y+maxDiff);
        
    }
}
