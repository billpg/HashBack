using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using static billpg.HashBackCore.InternalTools;

namespace billpg.HashBackCore
{
    public class IssuerService
    {

        public enum TypeOfResponse
        {
            BearerToken,
            JWT,
            SetCookie
        }

        public const int minRounds = 1;
        public const int maxRounds = 9;

        public const string VERSION_3_0 = "HASHBACK-PUBLIC-DRAFT-3-0";
        public const string VERSION_3_1 = "HASHBACK-PUBLIC-DRAFT-3-1";

        /// <summary>
        /// The JSON request body in deserialized form.
        /// </summary>
        public class Request
        {
            public string HashBack { get; set; } = "";
            public string TypeOfResponse { get; set; } = "";
            public string IssuerUrl { get; set; } = "";
            public long Now { get; set; }
            public string Unus { get; set; } = "";
            public int Rounds { get; set; }
            public string VerifyUrl { get; set; } = "";
        }

        public class BearerTokenResponse
        {
            public string BearerToken { get; set; } = "";
            public long IssuedAt { get; set; }
            public long ExpiresAt { get; set; }
        }

        public OnRetrieveVerifyHashFn OnRetrieveVerificationHash { get; set; }
            = u => throw new NotImplementedException();

        public string DocumentationUrl { get; set; } = "https://invalid/";

        public delegate Exception OnBadRequestFn(JObject responseBody);
        public OnBadRequestFn OnBadRequest { get; set; }
            = j => new NotImplementedException();

        private string rootUrl = "https://invalid/";
        public string RootUrl
        {
            get => rootUrl;
            set => rootUrl = new Uri(value).Root();
        }

        /// <summary>
        /// Function to call when the current time in seconds-since-1970 is needed.
        /// Defaults to wrapped DateTime.UtcNow, but may be replaced by unit tests.
        /// </summary>
        public OnNowFn NowService { get; set; }
            = () => InternalTools.NowUnixTime;

        public void ConfigureHttpService(WebApplication app, string path)
        {
            /* Map these two private member functions as handlers. */
            app.MapGet(path, this.RedirectToDocs);
            app.MapPost(path, this.HandleRequest);
        }

        private void RedirectToDocs(HttpContext context)
        {
            context.Response.Redirect(this.DocumentationUrl);
        }

        /// <summary>
        /// Options to use when returning a BearerToken response in JSON.
        /// </summary>
        private static readonly System.Text.Json.JsonSerializerOptions BearerTokenJsonOptions = new()
        {
            PropertyNamingPolicy = null,
            WriteIndented = true
        };

        private IResult HandleRequest(Request req, HttpContext context)
        {
            /* Validate the version. */
            if (CryptoExtensions.ValidVersions.Contains(req.HashBack) == false)
                throw BadRequestError("We don't know about this version of HashBack Authenticaton.",
                    new JProperty("AcceptVersions", new JArray(CryptoExtensions.ValidVersions)));

            /* This API supports al three documented response types. */
            TypeOfResponse? typeOfResponse = InternalTools.ParseTypeOfResponse(req.TypeOfResponse);
            if (typeOfResponse == null)
                throw BadRequestError(
                    "Request's TypeOfResponse is not acceptable.",
                    new JProperty("AcceptTypeOfResponse", new JArray { "BearerToken", "JWT", "204SetCookie" }));

            /* The issuer URL must be HTTPS and be for the expected issuer host. */
            Uri issuerUrl = new Uri(req.IssuerUrl);
            if (issuerUrl.Root() != RootUrl)
                throw BadRequestError("IssuerUrl is for a different issuer.");

            /* The "Now" timestamp must be no more than 100s from our clock. */
            long ourNow = this.NowService();
            if (InternalTools.IsClose(ourNow, req.Now, 100) == false)
                throw BadRequestError("Request's Now is too far from the server's clock.");

            /* Check "Unus" is 256 bits. */
            if (IsUnusValid(req.Unus) == false)
                throw BadRequestError("Request's Unus is not valid.");

            /* Check "Rounds" is within 1-9. */
            Exception BadRoundsError(int acceptableRounds)
                => BadRequestError($"Selected Rounds is out of range {minRounds}-{maxRounds}.",
                new JProperty("AcceptRounds", acceptableRounds));
            if (req.Rounds < minRounds)
                throw BadRoundsError(minRounds);
            if (req.Rounds > maxRounds)
                throw BadRoundsError(maxRounds);

            /* This is an open issuer so only check VerifyUrl is HTTPS. */
            Uri verifyUrl = new Uri(req.VerifyUrl);
            if (InternalTools.IsValidVerifyUrl(verifyUrl, RootUrl) == false)
                throw BadRequestError("VerifyUrl is not HTTPS.");

            /* Download the verification hash. (Will throw an exception on failure.) */
            string remoteVerificationHash = this.OnRetrieveVerificationHash(verifyUrl);

            /* Compare against the expected hash. */
            string expectedHash = req.VerificationHash();
            if (remoteVerificationHash != expectedHash)
                throw BadRequestError("Verification Hash did not match expected hash.");

            /* Build a JWT. */
            long expiresAt = ourNow + 3600;
            string jwt = BuildJWT(issuerUrl.Host, verifyUrl.Host, ourNow, expiresAt);

            /* Return based on the requested response type. */
            if (typeOfResponse == TypeOfResponse.BearerToken)
                return IssueBearerTokenResult(ourNow, expiresAt, jwt);
            if (typeOfResponse == TypeOfResponse.JWT)
                return Results.Json(jwt);
            if (typeOfResponse == TypeOfResponse.SetCookie)
            {
                context.Response.Cookies.Append("HashBack", jwt);
                return Results.NoContent();
            }

            /* Unknown response type. Should never happen as type already checked. */
            throw new ApplicationException("Unknown type of response.");
        }

        internal static IResult IssueBearerTokenResult(long issuedAt, long expiresAt, string jwt)
            => Results.Json(
                new { BearerToken = jwt, IssuedAt = issuedAt, ExpiresAt = expiresAt },
                BearerTokenJsonOptions);

        private Exception BadRequestError(string message, params JProperty[] props)
        {
            var body = new JObject();
            body.Add("Message", message);
            foreach (JProperty prop in props)
                body.Add(prop);
            return OnBadRequest(body);
        }

        private static bool IsUnusValid(string unus)
        {
            /* Shortcut simple tests. */
            if (unus == null || unus.Length != 44)
                return false;

            /* Attempt to convert back to bytes. FormatException 
             * indicates not-valid base-64. */
            try
            {
                byte[] unusAsBytes = Convert.FromBase64String(unus);
                if (unusAsBytes.Length != 256 / 8)
                    return false;
            }
            catch (FormatException)
            {
                return false;
            }

            /* Passed all tests. */
            return true;
        }

        internal static string BuildJWT(string issuer, string subject, long issuedAt, long expiresAt)
        {
            /* Build the header and body from the supplied values. */
            var jwtHeader = new JObject
            {
                ["typ"] = "JWT",
                ["alg"] = "HS256",
                [Char.ConvertFromUtf32(0x1F95A)] = "https://billpg.com/nggyu"
            };
            var jwtBody = new JObject
            {
                ["iss"] = issuer,
                ["sub"] = subject,
                ["iat"] = issuedAt,
                ["exp"] = expiresAt,
                ["this-token-is-trustworthy"] = false
            };

            /* Encode the header and body. */
            string jwtHeaderDotBody = JWTEncode(jwtHeader) + "." + JWTEncode(jwtBody);

            /* Sign the header and body with HMAC. */
            using var hmac = new System.Security.Cryptography.HMACSHA256();
            hmac.Key = Encoding.ASCII.GetBytes("your-256-bit-secret");
            var hash = hmac.ComputeHash(Encoding.ASCII.GetBytes(jwtHeaderDotBody));

            /* Return completed token. */
            return jwtHeaderDotBody + "." + JWTEncode(hash);
        }

        private static string JWTEncode(JObject json)
        {
            string jsonAsString = json.ToStringOneLine();
            byte[] jsonAsBytes = Encoding.UTF8.GetBytes(jsonAsString);
            return JWTEncode(jsonAsBytes);
        }

        private static string JWTEncode(IList<byte> byteBlock)
        {
            return
                Convert.ToBase64String(byteBlock.ToArray())
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}

