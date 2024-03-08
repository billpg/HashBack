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
        private const int hexTokenLength = 256 / 4;
        private const int hexTokenWithExtLength = hexTokenLength + 4;
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);

        internal static void GetStoreHash(HttpContext context)
        {
            /*
            curl -i http://localhost:3001/devHashStore/load/xyz/abc.txt
            */

            /* Load the two strings from the URL. If either are missing, 404. */
            string? userAsHex = context.Request.RouteValues["user"] as string;
            string? fileAsHex = context.Request.RouteValues["file"] as string;
            if (userAsHex == null || userAsHex.Length != hexTokenLength || 
                fileAsHex == null || fileAsHex.Length != hexTokenWithExtLength)
                throw new NotFoundException();

            /* Reject filenames that don't end in .txt and remove the suffix.
             * (We know the stringis long enough thanks to the above test.) */
            if (fileAsHex.Substring(hexTokenLength) != ".txt")
                throw new NotFoundException();
            fileAsHex = fileAsHex.Substring(0, hexTokenLength);

            /* Query dev hash store. */
            byte[] hashAsBytes = DevHashStore.Load(userAsHex, fileAsHex);
            if (hashAsBytes == null)
                throw new NotFoundException();

            /* Return hash. */
            context.Response.StatusCode = 200;
            context.Response.Headers["Content-Type"] = "text/plain";
            context.Response.Body.WriteAsync(UTF8.GetBytes(Convert.ToBase64String(hashAsBytes) + "\r\n"));
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
            string localPath = DevHashStore.Store(user, name, hashAsBytes);

            /* Return a "created" response. */
            context.Response.StatusCode = 201;
            context.Response.Headers["Location"] = context.Request.Url().WithRelativePath("load/").WithRelativePath(localPath).ToString();
            context.Response.Headers["Content-Type"] = "text/plain";
            context.Response.Body.WriteAsync(UTF8.GetBytes(hash + "\r\n"));
        }
    }
}
