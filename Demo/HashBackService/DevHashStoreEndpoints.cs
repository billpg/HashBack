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
        private static readonly UTF8Encoding UTF8 = new UTF8Encoding(false);
        private static readonly string expectedHost = ServiceConfig.LoadRequiredString("IssuerHost");
        private static readonly string userHashSecret =
            ServiceConfig.LoadStringOrSet(
                "HashStoreUserHashSecret",
                CryptoExtensions.GenerateUnus);

        internal static void GetUser(HttpContext context)
        {
            /*
            curl -i http://localhost:3001/devHashStore/user?Name=rutabaga84
            */

            /* Load the username from the query string. */
            string? user = context.Request.Query["Name"];
            if (user == null)
                throw new BadRequestException("Missing required ?Name= query string parameter.");
            if (user.Length < 10)
                throw new BadRequestException("Name query string parameter must be at least 10 characters.");

            /* Hash the user string. */
            string userHashed = CryptoExtensions.UserToHashedUser(user, userHashSecret);

            /* Build response. */
            context.Response.StatusCode = 200;
            context.Response.Headers["Content-Type"] = "application/json";
            JObject responseBody = new JObject
            {
                ["//1"] = "Thank you. Your user folder is",
                ["UserFolder"] = userHashed,
                ["//2"] = "which means your VerifyUrl must start",
                ["VerifyUrlStartsWith"] = $"https://{expectedHost}/devStoreHash/load/{userHashed}/",
                ["//3"] = "and end with a number with 4-10 digits and \".txt\"."
            };
            context.Response.Body.WriteAsync(UTF8.GetBytes(responseBody.ToStringIndented()));
        }

        internal static void GetStoreHash(HttpContext context)
        {
            /*
            curl -i http://localhost:3001/devHashStore/load/2HJckET4vl/84848484.txt
            */

            /* Load the two strings from the URL. If either are missing, 404. */
            string? userHashed = context.Request.RouteValues["user"] as string;
            string? filename = context.Request.RouteValues["file"] as string;
            if (userHashed == null || userHashed.Length != 10 ||
                filename == null || filename.Length < 8)
                throw new NotFoundException();

            /* Reject filenames that don't end in .txt and remove the suffix.
             * (We know the stringis long enough thanks to the above test.) */
            if (filename.EndsWith(".txt") == false)
                throw new NotFoundException();
            filename = filename.Substring(0, filename.Length - 4);

            /* Reject filenames that don't parse. */
            if (long.TryParse(filename, out long filenameAsInt) == false)
                throw new NotFoundException();

            /* Query dev hash store. */                       
            string hash = DevHashStore.Load(userHashed, filenameAsInt);
            if (hash == null)
                throw new NotFoundException();

            /* Return hash. */
            context.Response.StatusCode = 200;
            context.Response.Headers["Content-Type"] = "text/plain";
            context.Response.Body.WriteAsync(UTF8.GetBytes(hash + "\r\n"));
        }

        internal static void PostStore(HttpContext context)
        {
            /* 
            curl -i --request POST --header "Content-Type: application/json" --data "{\"user\":\"billpg\",\"name\":\"xyz\",\"hash\":\"hdytfrkrysge7ufhrnstyuihhe749jshTHJOGHJLK3H=\"}" http://localhost:3001/devHashStore/store 
            */

            /* Load request body. */
            using var ms = new MemoryStream();
            context.Request.Body.CopyToAsync(ms).Wait();
            var req = JObject.Parse(Encoding.UTF8.GetString(ms.ToArray()));

            /* Load the request JSON into a request object. */
            var reqParsed = CallerRequest.Parse(req);

            /* Pull out the "user" request parameter and hash it. */
            string? user = req["User"]?.Value<string>();
            if (user == null || user.Length == 0)
                throw new BadRequestException("Missing required User property.");
            if (user.Length < 10)
                throw new BadRequestException("User must be at least 10 characters.");
            string userHashed = CryptoExtensions.UserToHashedUser(user, userHashSecret);

            /* Validate the "Rounds" property as within range. */
            if (reqParsed.Rounds < 1 || reqParsed.Rounds > 9)
                throw new BadRequestException("Rounds request property must be 1-9.");

            /* Pull out the expected host from config. */

            /* Helper function to build a bad-request for a bad VeirfyUrl value. */
            BadRequestException BadVerifyUrl()
                => new BadRequestException(
                    $"VerifyUrl must be form of " +
                    $"https://{expectedHost}/devStoreHash/load/(user)/(number).txt");

            /* Validate the VerifyUrl and pull out the filename along the way. */
            var verifyUrl = new Uri(reqParsed.VerifyUrl);
            if (verifyUrl.Scheme != "https")
                throw BadVerifyUrl();
            if (verifyUrl.Host != expectedHost)
                throw BadVerifyUrl();
            if (verifyUrl.Query != "")
                throw BadVerifyUrl();
            var verifyPathNodes = verifyUrl.AbsolutePath.Split('/').ToList();
            if (verifyPathNodes.Count != 5)
                throw BadVerifyUrl();
            if (verifyPathNodes[0] != "")
                throw BadVerifyUrl();
            if (verifyPathNodes[1] != "devStoreHash")
                throw BadVerifyUrl();
            if (verifyPathNodes[2] != "load")
                throw BadVerifyUrl();
            if (verifyPathNodes[3] != userHashed)
                throw BadVerifyUrl();
            var filename = verifyPathNodes[4];

            /* Validate the filename. */
            if (filename.Length < 6 || filename.EndsWith(".txt") == false)
                throw BadVerifyUrl();
            filename = filename.Substring(0, filename.Length - 4);
            if (long.TryParse(filename, out long filenameAsInt) == false ||
                filenameAsInt < 10000 ||
                filename != filenameAsInt.ToString())
                throw BadVerifyUrl();

            /* Add the hash to the store. */
            string hash = reqParsed.VerificationHash();
            DevHashStore.Store(userHashed, filenameAsInt, hash);

            /* Return a "created" response. */
            context.Response.StatusCode = 200;
            context.Response.Headers["Content-Type"] = "application/json";
            JObject responseBody = new JObject
            {
                ["//1"] = "We're happy to store your hash. The hash is",
                ["Hash"] = hash,
                ["//2"] = "and is available at",
                ["VerifyUrl"] = verifyUrl,
                ["//3"] = "but remember it'll be deleted on the first GET."
            };
            context.Response.Body.WriteAsync(UTF8.GetBytes(responseBody.ToStringIndented()));
        }
    }
}
