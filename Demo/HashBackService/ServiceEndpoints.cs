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
    internal static class ServiceEndpoints
    {
        internal class PostStoreHashResponse
        {
            public string Hello { get; } = "Hi";
        }

        internal static void PostStoreHash(HttpContext context)
        {
            /* 
            curl -i --request POST --header "Content-Type: application/json" --data "{\"user\":\"billpg\",\"name\":\"xyz\",\"hash\":\"hdytfrkrysge7ufhrnstyuihhe749jshTHJOGHJLK3H=\"}" http://localhost:3001/devHashStore/store 
            */

            /* Load request body. */
            using var ms = new MemoryStream();
            context.Request.Body.CopyToAsync(ms).Wait();
            var req = JToken.Parse(Encoding.UTF8.GetString(ms.ToArray()));
            if (req == null || req is not JObject)
                throw new BadRequestException("Request body must be a JSON object.");

            /* Pull out the request parameters. */
            string? user = req["user"]?.Value<string>();
            if (user == null || user.Length == 0)
                throw new BadRequestException("Missing required user property.");

            string? name = req["name"]?.Value<string>();
            if (name == null || name.Length == 0)
                throw new BadRequestException("Missing required name property.");

            string? hash = req["hash"]?.Value<string>();
            if (hash == null)
                throw new BadRequestException("Missing required hash property.");

            /* Validate the hash. */
            byte[] hashAsBytes = new byte[256 / 8];
            if (Convert.TryFromBase64String(hash, hashAsBytes, out int hashSizeInBytes) == false
                || hashSizeInBytes != 256/8)
                throw new BadRequestException("Hash property must be 256 bits, base-64 encoded.");

            /* Add the hash to the store. */
            DevHashStore.Store(user, name, hash);
        }
    }
}
