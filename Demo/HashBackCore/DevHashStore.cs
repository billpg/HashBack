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
            public readonly string Hash;
            public readonly long ExpiresAt;

            public StoredHash(string hash)
            {
                this.Hash = hash;
                this.ExpiresAt = DateTime.UtcNow.ToUnixTime() + 100;
            }
        }        

        /// <summary>
        /// Store of hashes. Lock before use.
        /// </summary>
        private static readonly Dictionary<string, StoredHash> hashes
            = new Dictionary<string, StoredHash>();

        /// <summary>
        /// List of hash keys in order of adding, so old ones can be shifted off.
        /// </summary>
        private static readonly List<string> hashKeysInOrder
            = new List<string>();

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

        private static string HashKey(string userHashed, long filename)
            => $"cache/{userHashed}/{filename}";

        public static void Store(string userHashed, long filename, string hash)
        {
            /* Store in memory store. */
            var storedHash = new StoredHash(hash);
            lock (hashes)
            {
                /* Store the new key. */
                string key = HashKey(userHashed, filename);
                hashes[key] = storedHash;
                hashKeysInOrder.Add(key);

                /* Remove all the older ones until the size is acceptable. */
                while (hashKeysInOrder.Count > 9999)
                {
                    string keyToFlush = hashKeysInOrder[0];
                    hashes.Remove(keyToFlush);
                    hashKeysInOrder.Remove(keyToFlush);
                }
            }
        }

        public static string Load(string userHashed, long filename)
        {
            /* Read from the store and remove. */
            StoredHash? storedHash;
            lock (hashes)
            {               
                string key = HashKey(userHashed, filename);
                if (hashes.TryGetValue(key, out storedHash) == false || storedHash == null)
                    throw new ApplicationException("Not Found");
                hashes.Remove(key);
            }

            /* Check expiry. */
            if (storedHash.ExpiresAt < DateTime.UtcNow.ToUnixTime())
                throw new ApplicationException("Not Found");

            /* Return hash. */
            return storedHash.Hash;
        }
    }
}
