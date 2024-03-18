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

namespace billpg.HashBackService
{
    internal static class DevHashStoreEndpoints
    {
        /// <summary>
        /// The root URL of this service.
        /// </summary>
        private static readonly string rootUrl = ServiceConfig.LoadRequiredString("RootUrl");

        private static readonly IList<byte> responseBodyAsBytes =
            Encoding.UTF8.GetBytes(
                "This is an open hash storage service for testing HashBack implementations\r\n" +
                "only. It is not suitable for production use, nor for any purpose that\r\n" +
                "requires security. Use your website where only you and people you trust\r\n" +
                "have control over what files are published to store your hashes.\r\n" +
                "\r\n" +
                "Regards, Bill, billpg.com. \uD83E\uDD89\r\n"
                ).AsReadOnly();

        /// <summary>
        /// Encapsulated hash storage.
        /// </summary>
        private static class HashStorage
        {
            /// <summary>Monitor object to lock while accesinng.</summary>
            private static readonly object monitor = new object();

            /// <summary>Collection of hashes stored.</summary>
            private static readonly Dictionary<Guid, IList<byte>> hashes                
                = new Dictionary<Guid, IList<byte>>();

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
            public static void Store(Guid id, IList<byte> hash)
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

            public static IList<byte>? Load(Guid id)
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

        internal static void AddHash(HttpContext context)
        {
            /* Load request body. */
            using var ms = new MemoryStream();
            context.Request.Body.CopyToAsync(ms).Wait();
            var req = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));

            /* Pull out the ID property and validate. */
            string idAsString = LoadPropertyOrBadRequest(req, "ID");
            if (Guid.TryParse(idAsString, out Guid id) == false)
                throw new BadRequestException("ID property is not a valid UUID.");

            /* Pull out the Hash property and validate. */
            string? hashAsString = LoadPropertyOrBadRequest(req, "Hash");
            var hashAsBytes = ConvertFromBase64OrNull(hashAsString, 256/8);
            if (hashAsBytes == null)
                throw new BadRequestException("Hash must be 256 bits of BASE64.");

            /* Save hash. (This may throw a 400 exception.) */
            HashStorage.Store(id, hashAsBytes);

            /* Return a success response. */
            context.Response.StatusCode = 200;
            context.Response.Headers["Content-Type"] = "text/plain";
            context.Response.Body.WriteAsync(responseBodyAsBytes.ToArray());
        }

        internal static void GetHash(HttpContext context)
        {
            /* Load the ID query string parameter. If not used, redirect to the documentation. */
            string? idAsString = context.Request.Query["ID"];
            if (idAsString == null)
                context.Response.Redirect(ServiceConfig.LoadRequiredString("RedirectHashStoreTo"));
            if (Guid.TryParse(idAsString, out Guid id) == false)
                throw new BadRequestException("ID query string is not a valid UUID.");

            /* Load hash from store. */
            var hash = HashStorage.Load(id);
            if (hash == null)
                throw new BadRequestException("No hash with this ID.");

            /* Return hash. */
            context.Response.StatusCode = 200;
            context.Response.Headers["Content-Type"] = "text/plain";
            string hashAsString = Convert.ToBase64String(hash.ToArray());
            context.Response.Body.WriteAsync(Encoding.ASCII.GetBytes(hashAsString + "\r\n"));
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
