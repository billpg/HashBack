using billpg.HashBackCore;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public class AuthorizationHeaderService
    {
        public Uri RootUrl { get; set; } = null!;
        public int ClockMarginSeconds { get; set; } = 10;
        public int IssuedTokenLifespanSeconds { get; set; } = 1000;
        public OnErrorFn OnBadRequest { get; set; }
            = msg => throw new NotImplementedException();
        public OnReadClockFn OnReadClock { get; set; }
            = () => throw new NotImplementedException();
        public OnRetrieveVerifyHashFn OnRetrieveVerifyHash { get; set; }
            = url => throw new NotImplementedException();

        public OnAuthorizationHeaderFn Handle => this.HandleInternal;
        private (string subject, long serverNow) HandleInternal(string authHeader)
        {
            /* Parse the Authorization header. Must have at least two parts. */
            var authBySpace =
                authHeader
                .Split(' ', '\r', '\n', '\t')
                .Where(s => s.Length > 0)
                .ToList();
            if (authBySpace.Count < 2)// ||
                throw OnBadRequest("Authorization header must have two parts.");

            /* Extract authorizaton type and payload. If payload is in many parts, rejoin them. */
            string authType = authBySpace[0];
            string authPayload = string.Concat(authBySpace.Skip(1));

            /* Is this a HashBack or Bearer header? */
            if (authType == "HashBack")
                return ParseHashBackAuthHeader(authPayload);
            else if (authType == "Bearer")
                return ValidateJWT(authPayload);
            else
                throw OnBadRequest("Unknown Authorization type.");
        }

        (string subject, long serverNow) ParseHashBackAuthHeader(string authPayload)
            {
                /* Convert the payload from BASE-64 to bytes. */
                var jsonAsBytes = ParseBase64OrNull(authPayload);
                if (jsonAsBytes == null)
                    throw OnBadRequest("HashBack payload must be BASE-64 encoded JSON.");

                /* Convert the header payload to JSON. */
                JObject? authJson = ParseJsonOrNull(jsonAsBytes);
                if (authJson == null)
                    throw OnBadRequest("HashBack payload must be BASE-64 encoded JSON.");

                /* Check the Host property. */
                string? authHost = authJson["Host"]?.Value<string>();
                if (authHost == null)
                    throw OnBadRequest("Missing Host property in JSON.");
                if (authHost != RootUrl.Host)
                    throw OnBadRequest("Wrong Host property in JSON. Expected=" + RootUrl.Host);

                /* Check the Now property. */
                long serverNow = OnReadClock();
                long? clientNow = authJson["Now"]?.Value<long>();
                if (clientNow == null)
                    throw OnBadRequest("Missing Now property in JSON.");
                if (clientNow < serverNow - ClockMarginSeconds ||
                    clientNow > serverNow + ClockMarginSeconds)
                    throw OnBadRequest("Now property is too far from now. Expected=" + serverNow);

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

                /* Check Verify. (This checks the basic validity of a URL.
                 * The download service will perform its own checks.) */
                string? verifyAsString = authJson["Verify"]?.Value<string?>();
                if (verifyAsString == null)
                    throw OnBadRequest("Missing Verify property in JSON.");
                Uri? verifyAsUrl = ParseUrlOrNull(verifyAsString);
                if (verifyAsUrl == null)
                    throw OnBadRequest("Verify property is not a valid URL.");

                /* Download verification hash. Will throw exception if it can't. */
                string verifyHashAsString = OnRetrieveVerifyHash(verifyAsUrl);
                var verifyHashAsBytes = HashService.ConvertFromBase64OrNull(verifyHashAsString, 256 / 8);
                if (verifyHashAsBytes == null)
                    throw OnBadRequest("Result of downloading hash was not 256 bits in base-64 form.");

                /* Find the expected hash from the bytes inside the BASE64 block. */
                string expectedVerifyHash = CryptoExtensions.HashBytesPerFourDotZero(jsonAsBytes, rounds.Value);

                /* Do they match? */
                if (verifyHashAsString != expectedVerifyHash)
                    throw OnBadRequest("Downloaded verification hash did not match the expected hash.");

                /* They matched. Return the verified caller's domain. */
            return (verifyAsUrl.Host, serverNow);
        }

        (string subject, long serverNow) ValidateJWT(string jwt)
        {
            /* Resuable error message for a bad bearer-token. */
            const string notValidError = "Bearer token is not valid.";

            /* JWT must be in three dot-separated parts. */
            string[] jwtByDot = jwt.Split('.');
            if (jwtByDot.Length != 3)
                throw OnBadRequest(notValidError);

            /* Find the expected signature by hashing the header and body. */
            var expectedSignature = JWT.Sign(jwtByDot[0] + "." + jwtByDot[1]);

            /* Reject if the supplied signature doesn't match. */
            if (jwtByDot[2] != expectedSignature)
                throw OnBadRequest(notValidError);

            /* Parse the header and body. */
            JObject? headerAsJson = ParseBase64AsJsonOrNull(jwtByDot[0]);
            JObject? bodyAsJson = ParseBase64AsJsonOrNull(jwtByDot[1]);
            if (headerAsJson == null || bodyAsJson == null)
                throw OnBadRequest(notValidError);

            /* Check the expiry. */
            long? expiresAt = bodyAsJson["exp"]?.Value<long>();
            if (expiresAt == null)
                throw OnBadRequest(notValidError);
            long serverNow = this.OnReadClock();
            if (serverNow > expiresAt)
                throw OnBadRequest("Bearer token has expired.");

            /* Check the "aud" claim matches our root url. */
            var audience = bodyAsJson["aud"]?.Value<string>();
            if (audience == null || audience != RootUrl.Host)
                throw OnBadRequest(notValidError);

            /* Pull out the "sub" claim. */
            var subject = bodyAsJson["sub"]?.Value<string>();
            if (subject == null)
                throw OnBadRequest(notValidError);

            /* Return validated subject claim. */
            return (subject, serverNow);
        }

        private static JObject? ParseBase64AsJsonOrNull(string payload)
        {
            /* First decode BASE-64 to bytes. */
            var payloadAsBytes = ParseBase64OrNull(payload);
            if (payloadAsBytes == null)
                return null;

            /* Parse bytes as JSON. Will return null if not valid. */
            return ParseJsonOrNull(payloadAsBytes);
        }

        private static IList<byte>? ParseBase64OrNull(string payload)
        {
            /* If the payload contains braces, it might already be JSON. 
             * Return the bytes to complete the loop. */
            if (payload.Contains('{'))
                return Encoding.ASCII.GetBytes(payload);

            /* Otherwise, it'll be BASE-64 encoded. Decode and return bytes. 
             * (If not valid BASE-64 return null indicating as such.) */
            return HashService.ConvertFromBase64OrNull(payload);
        }

        private static JObject? ParseJsonOrNull(IList<byte> jsonAsBytes)
        {
            /* Convert bytes to the new JSON payload and continue as if the JSON 
             * was in the header directly. (This won't throw if the bytes are not
             * valid UTF-8 but will instead insert question marks which will fail
             * validation farther down the line.) */
            string jsonAsString = Encoding.UTF8.GetString(jsonAsBytes.ToArray());

            /* Attempt to convert string to a JObject. Return null if not valid JSON. */
            try
            {
                return JObject.Parse(jsonAsString);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return null;
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


    }
}
