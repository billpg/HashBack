using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Linq.Expressions;

namespace billpg.HashBackCore
{
    public class BearerTokenService
    {
        public string RootUrl { get; set; } = "";
        public int NowValidationMarginSeconds { get; set; } = 10;
        public int IssuedTokenLifespanSeconds { get; set; } = 1000;
        public OnErrorFn OnBadRequest { get; set; }
            = msg => throw new NotImplementedException();
        public OnReadClockFn OnReadClock { get; set; }
            = () => throw new NotImplementedException();
        public OnRetrieveVerifyHashFn OnRetrieveVerifyHash { get; set; }
            = url => throw new NotImplementedException();

        public void ConfigureHttpService(WebApplication app, string path)
        {
            app.MapGet(path, this.GetHandler);
        }

        private IResult GetHandler(
            [FromHeader(Name = "Authorization")] string? authHeader,
            [FromHeader(Name = "Accept")] string? acceptHeader,
            HttpContext context)
        {
            /* Respond to queries without an Authorizaton header with a 401. */
            if (string.IsNullOrEmpty(authHeader))
            {
                context.Response.Headers.Append("WWW-Authenticate", "HashBack");
                return Results.Text(content: "Missing Authorization header.", statusCode: 401);
            }

            /* If Accept header is missing, add default. */
            if (string.IsNullOrEmpty(acceptHeader))
                acceptHeader = "text/plain";

            /* Pull out the remote IP and check block-list. */
            IPAddress remoteIp = context.RemoteIP();
            //TODO: Check the block list.

            /* Parse the Authorization header. (Or will throw bad-request exception.) */
            (var authJson, var jsonAsBytes) = ParseAuthorization(authHeader);

            /* Check the Host property. */
            string? authHost = authJson["Host"]?.Value<string>();
            if (authHost == null)
                throw OnBadRequest("Missing Host property in JSON.");
            string expectedHost = new Uri(RootUrl).Host;
            if (authHost != expectedHost)
                throw OnBadRequest("Wrong Host property in JSON. Expected=" + expectedHost);

            /* Check the Now property. */
            long? authNow = authJson["Now"]?.Value<long>();
            if (authNow == null)
                throw OnBadRequest("Missing Now property in JSON.");
            long actualNow = OnReadClock();
            if (authNow < actualNow - NowValidationMarginSeconds || 
                authNow > actualNow + NowValidationMarginSeconds)
                throw OnBadRequest("Now property is too far from now. Expected=" + actualNow);

            /* Check Unus. */
            string? unusAsString = authJson["Unus"]?.Value<string>();
            if (unusAsString == null)
                throw OnBadRequest("Missing Unus property in JSON.");
            var unusAsBytes = HashService.ConvertFromBase64OrNull(unusAsString, 256 / 8);
            if (unusAsString == null)
                throw OnBadRequest("Unus property must be 256 bits, base64 encoded.");

            /* Check Rounds. */
            int? rounds = authJson["Rounds"]?.Value<int>();
            if (rounds == null)
                throw OnBadRequest("Missing Rounds property in JSON.");
            if (rounds < 1 || rounds > 99)
                throw OnBadRequest("Rounds must be 1-99.");

            /* Check Verify. */
            string? verifyAsString = authJson["Verify"]?.Value<string?>();
            if (verifyAsString == null)
                throw OnBadRequest("Missing Verify property in JSON.");
            Uri? verifyAsUrl = ParseUrlOrNull(verifyAsString);
            if (verifyAsUrl == null)
                throw OnBadRequest("Verify property is not valid.");
            if (verifyAsUrl.Scheme != "https" && verifyAsUrl.Root() != RootUrl)
                throw OnBadRequest("Verify property must be HTTPS.");
            if (verifyAsUrl.Host == "localhost" && new Uri(RootUrl).Host != "localhost")
                throw OnBadRequest("Verify property must not be localhost.");
            if (verifyAsUrl.Host != "localhost" && verifyAsUrl.Port != 443)
                throw OnBadRequest("Verify property must use port 443.");
            if (IPAddress.TryParse(verifyAsUrl.Host, out _))
                throw OnBadRequest("Verify must not use an IP address as host.");            

            /* Download verification hash. Will throw exception if it can't. */
            string verifyHashAsString = OnRetrieveVerifyHash(verifyAsUrl);

            /* Find the expected hash from the bytes inside the BASE64 block. */
            string expectedVerifyHash = CryptoExtensions.ExpectedHashFourDotZero(jsonAsBytes, rounds.Value);

            /* Do they match? */
            if (verifyHashAsString != expectedVerifyHash)
                throw OnBadRequest("Downloaded verification hash did not match the expected hash.");

            /* They match. Return an issued bearer token. */
            long expiresAt = actualNow + IssuedTokenLifespanSeconds;
            string jwt = IssuerService.BuildJWT(expectedHost, verifyAsUrl.Host, actualNow, expiresAt);
            return IssuerService.IssueBearerTokenResult(actualNow, expiresAt, jwt);
        }

        private (JObject json, IList<byte> jsonAsBytes) ParseAuthorization(string auth)
        {
            /* Split by spaces, first part must be "HashBack". */
            var authBySpace =
                auth
                .Split(' ', '\r', '\n', '\t')
                .Where(s => s.Length > 0)
                .ToList();
            if (authBySpace.Count < 1 || authBySpace[0] != "HashBack")
                throw OnBadRequest("Authorization header must start with 'HashBack'.");

            /* Remove "HashBack" and join the remaining parts together. */
            string jsonAsBase64 = string.Concat(authBySpace.Skip(1));

            /* Try converting base64 to bytes. */
            IList<byte>? jsonAsBytes = HashService.ConvertFromBase64OrNull(jsonAsBase64);
            if (jsonAsBytes == null)
                throw OnBadRequest("Authorization header must start with 'Hashback'.");

            /* Convert bytes to a string. */
            string jsonAsString = Encoding.UTF8.GetString(jsonAsBytes.ToArray());

            /* Attempt to convert string to a JObject. Throw bad-request if we can't. */
            try
            {
                return (JObject.Parse(jsonAsString), jsonAsBytes);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                throw OnBadRequest("Authorization header uses invalid JSON.");
            }
        }

        private static Uri? ParseUrlOrNull(string url)
        {
            /* Attempt to parse URL. */
            try
            {
                return new Uri(url);
            }
            catch (UriFormatException)
            {
                return null;
            }
        }

        private static bool IsIpAddress(string host)
        {
            throw new NotImplementedException();
        }
    }
}
