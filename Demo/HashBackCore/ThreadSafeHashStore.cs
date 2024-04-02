using billpg.HashBackCore;
using billpg.UsefulDataStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Stores hashes in a thread-safe manner on behalf of the hash-service api.
    /// </summary>
    internal class ThreadSafeHashStore
    {
        /// <summary>
        /// A single item in storage.
        /// </summary>
        internal struct StoredHash
        {
            public readonly IList<byte> Hash;
            public readonly IPAddress SenderIP;
            public readonly long SentAt;

            public StoredHash(IList<byte> hash, IPAddress senderIP, long sentAt)
            {
                this.Hash = hash;
                this.SenderIP = senderIP;
                this.SentAt = sentAt;
            }
        }

        /// <summary>
        /// Monitor object to lock while accesing.
        /// </summary>
        private readonly object monitor = new object();

        /// <summary>
        /// Collection of hashes stored.
        /// </summary>
        private readonly Dictionary<Guid, StoredHash> hashes
            = new Dictionary<Guid, StoredHash>();

        /// <summary>
        /// IDs waiting to be deleted. The longest waiting will be deleted first.
        /// </summary>
        private readonly Queue<Guid> waiting = new Queue<Guid>();

        /// <summary>
        /// Used black-listed IDs to prevent reuse of IDs.
        /// (Limited capacity to 100k.)
        /// </summary>
        private readonly LimitedCapacityHashSet<Guid> usedIDs 
            = new(maxHashCapacity * 10);

        /// <summary>
        /// Maximum number of hashes to store in memory.
        /// </summary>
        private const int maxHashCapacity = 9999;

        /// <summary>
        /// Store the supplied hash under the supplied ID, or throw if already used.
        /// </summary>
        /// <param name="id">ID to store hash.</param>
        /// <param name="hash">Hash to store.</param>
        /// <exception cref="IDAlreadyInUseException">Thrown if ID already used.</exception>
        internal void Store(Guid id, StoredHash hash, Func<Exception> onAlreadyInUse)
        {
            /* There can be only one! */
            lock (monitor)
            {
                /* Reject if key present or recently used. */
                if (hashes.ContainsKey(id) || usedIDs.Contains(id))
                    throw onAlreadyInUse();

                /* Store in collections. */
                hashes.Add(id, hash);
                waiting.Enqueue(id);

                /* Keep looping if over capacity... */
                while (hashes.Count > maxHashCapacity)
                {
                    /* Select the one waiting longest and remove it. */
                    Guid idToRemove = waiting.Dequeue();
                    hashes.Remove(idToRemove);

                    /* Add it to the blocked list. */
                    usedIDs.Add(idToRemove);
                }
            }
        }

        public StoredHash? Load(Guid id)
        {
            /* There can be only one! */
            lock (monitor)
            {
                /* Look for id. */
                if (hashes.TryGetValue(id, out var hash))
                {
                    /* Clear item to prevent re-gets and block for the future. */
                    hashes.Remove(id);
                    usedIDs.Add(id);

                    /* Return to caller. */
                    return hash;
                }

                /* Otherwise, return null for not found. */
                return null;
            }        
        }
    }
}
