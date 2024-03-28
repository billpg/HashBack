using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using billpg.WebAppTools;
using Newtonsoft.Json.Linq;
using billpg.HashBackCore;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace billpg.HashBackService
{
    internal static class DevHashStoreEndpoints
    {
        /// <summary>
        /// Encapsulated hash storage.
        /// </summary>
        private static class HashStorage
        {
            /// <summary>Monitor object to lock while accesing.</summary>
            private static readonly object monitor = new object();

            /// <summary>Collection of hashes stored.</summary>
            private static readonly Dictionary<Guid, StoredHash> hashes                
                = new Dictionary<Guid, StoredHash>();

            /// <summary>IDs waiting to be deleted.</summary>
            private static readonly Queue<Guid> waiting = new Queue<Guid>();

            /// <summary>Used black-listed IDs to prevent reuse of IDs.</summary>
            private static readonly HashSet<Guid> usedIDs = new HashSet<Guid>();

            /// <summary>Maximum number of hashes to store in memory.</summary>
            private const int maxHashCapacity = 9999;

            /// <summary>Maximum number of IDs to store.</summary>
            private const int maxIDCapacity = 99999;

            /// <summary>
            /// Store the supplied hash under the supplied ID, or throw a bad-request exception.
            /// </summary>
            /// <param name="id">ID to store hash.</param>
            /// <param name="hash">Hash to store.</param>
            /// <exception cref="BadRequestException">Thrown for any issue found.</exception>
            public static void Store(Guid id, StoredHash hash)
            {
                lock (monitor)
                {
                    /* Reject if key present or recently used. */
                    if (hashes.ContainsKey(id) || usedIDs.Contains(id))
                        throw new BadRequestException("ID is already in use or was recently used.");

                    /* Store in collections. */
                    hashes.Add(id, hash);
                    waiting.Enqueue(id);
                    usedIDs.Add(id);

                    /* If over capacity, remove the longest waiting. */
                    while (hashes.Count > maxHashCapacity)
                    {
                        Guid idToRemove = waiting.Dequeue();
                        hashes.Remove(idToRemove);
                    }

                    /* If usedID collection is over capacity, remove some. */
                    while (usedIDs.Count > maxIDCapacity)
                        usedIDs.Remove(usedIDs.First());
                }
            }

            public static StoredHash? Load(Guid id)
            {
                lock (monitor)
                {
                    /* Look for id. */
                    if (hashes.TryGetValue(id, out var hash))
                    {
                        /* Clear item so it can't be got again. */
                        hashes.Remove(id);

                        /* Return to caller. */
                        return hash;
                    }

                    /* Otherwise, return null for not found. */
                    return null;
                }
            }
        } 

        internal struct StoredHash
        {
            public readonly IList<byte> Hash;
            public readonly IPAddress SenderIP;
            public readonly long SentAt;

            public StoredHash(IList<byte> hash, IPAddress senderIP)
            {
                this.Hash = hash;
                this.SenderIP = senderIP;
                this.SentAt = InternalTools.NowUnixTime;
            }
        }

        internal static void AddHash(IHandlerProxy proxy)
        {
            /* Load request body. */
            var req = proxy.RequestJson();
            if (req == null)
                throw new BadHttpRequestException("Missing request body.");

            /* Pull out the ID property and validate. */
            string idAsString = LoadPropertyOrBadRequest(req, "ID");
            if (Guid.TryParse(idAsString, out Guid id) == false)
                throw new BadRequestException("ID property is not a valid UUID.");

            /* Pull out the Hash property and validate. */
            string? hashAsString = LoadPropertyOrBadRequest(req, "Hash");
            var hashAsBytes = ConvertFromBase64OrNull(hashAsString, 256 / 8);
            if (hashAsBytes == null)
                throw new BadRequestException("Hash must be 256 bits of BASE64.");

            /* Save hash. (This may throw a 400 exception.) */
            var hashRecord = new StoredHash(hashAsBytes, proxy.ClientIP);
            HashStorage.Store(id, hashRecord);

            /* Return a success response. */
            proxy.ResponseCode(200);
            proxy.ResponseText(
                "This is an open hash storage service for testing HashBack implementations\r\n" +
                "only. It is not suitable for production use, nor for any purpose that\r\n" +
                "requires security. Use your website where only you and people you trust\r\n" +
                "have control over what files are published to store your hashes.\r\n" +
                "\r\n" +
                "Regards, Bill, billpg.com. \uD83E\uDD89\r\n");
        }

        internal static string GetHash(
            [FromQuery(Name = "id")] string? idAsString, HttpContext context)
        {
            /* Load the ID query string parameter. If not used, redirect to the documentation. */
            if (idAsString == null)
                throw ErrorHandler.RedirectExceptionToTargetInConfig("RedirectHashStoreTo");

            /* Convert ID to UUID, reporting bad-request if not valid. */
            if (Guid.TryParse(idAsString, out Guid id) == false)
                throw ErrorHandler.BadRequestExceptionWithText("ID query string is not a valid UUID.");

            /* Load hash from store. */
            var hashRecord = HashStorage.Load(id);
            if (hashRecord == null)
                throw ErrorHandler.BadRequestExceptionWithText("No hash with this ID.");

            /* Respond to caller. */
            context.Response.Headers.Append("X-Sender-IP", hashRecord.Value.SenderIP.ToString());
            context.Response.Headers.Append("X-Sent-At", hashRecord.Value.SentAt.ToString());
            return Convert.ToBase64String(hashRecord.Value.Hash.ToArray());
        }

        private static string LoadPropertyOrBadRequest(JObject req, string key)
        {
            string? value = req[key]?.Value<string>();
            if (value == null)
                throw new BadRequestException($"Missing required property {key} in request.");
            return value;
        }

        private static IList<byte>? ConvertFromBase64OrNull(string hash, int expectedByteCount)
        {
            /* Attempt to convert string into bytes. */
            byte[] hashAsBytes;
            try
            {
                hashAsBytes = Convert.FromBase64String(hash);
            }
            catch (FormatException)
            {
                /* Not valid base-64, so return null. */
                return null;
            }

            /* If not expected byte count, return null. */
            if (hashAsBytes.Length != expectedByteCount)
                return null;

            /* Passed tests, return bytes in a read-only package. */
            return hashAsBytes.AsReadOnly();
        }
    }
}
