/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public static class DevHashStore
    {
        private class StoredHash
        {
            public readonly byte[] Hash;
            public readonly long ExpiresAt;

            public StoredHash(byte[] hash)
            {
                this.Hash = hash;
                this.ExpiresAt = DateTime.UtcNow.ToUnixTime() + 100;
            }
        }        

        /// <summary>
        /// 64k pre-initiualised stores of hashes.
        /// </summary>
        private static readonly Dictionary<string, StoredHash> hashes
            = new Dictionary<string, StoredHash>();

        /// <summary>
        /// Compute SHA256 hash from bytes.
        /// </summary>
        private static readonly Func<byte[], byte[]> ComputeSha256
            = System.Security.Cryptography.SHA256.Create().ComputeHash;

        /// <summary>
        /// Convert a string to UTF-8 bytes without a BOM.
        /// </summary>
        private static readonly Func<string, byte[]> GetUtf8Bytes
            = new UTF8Encoding(false).GetBytes;

        public static string Store(string user, string filename, byte[] hashAsBytes)
        {
            /* Replace the supplied strings with their hashed versions. */
            var folder = GetFolder(user, filename);                

            /* Store in memory store. */
            var storedHash = new StoredHash(hashAsBytes);
            lock (hashes)
            {
                hashes[folder] = storedHash;
            }

            /* Return the hshed path. */
            return folder;
        }

        public static byte[] Load(string hashedUser, string hashedFilename)
        {
            /* Validate the hashed for hex/length. */
            if (hashedUser.Length != 256 / 4
                || hashedFilename.Length != 256 / 4
                || hashedUser.All(IsHexDigit) == false
                || hashedFilename.All(IsHexDigit) == false)
                throw new ApplicationException("NotHex");

            /* Read from the store. */
            StoredHash storedHash;
            lock (hashes)
            {
                storedHash = hashes[$"{hashedUser}/{hashedFilename}.txt"];
            }

            /* Check expiry. */
            if (storedHash.ExpiresAt < DateTime.UtcNow.ToUnixTime())
                throw new ApplicationException("Not Found");

            /* Return hash. */
            return storedHash.Hash;
        }

        private static string GetFolder(string user, string filename)
        {
            return HashEncode(user) + "/" + HashEncode(filename) + ".txt";
        }

        private static string HashEncode(string from)
        {
            byte[] sourceAsBytes = GetUtf8Bytes(from + "hashback.billpg.com");
            byte[] hashBytes = ComputeSha256(sourceAsBytes);
            return string.Concat(hashBytes.Select(b => b.ToString("X2")));
        }

        private static bool IsHexDigit(char ch)
            => (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F');
        
    }
}
