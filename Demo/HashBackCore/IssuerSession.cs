/* Copyright William Godfrey, 2024. All rights reserved.
 * billpg.com
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;


namespace billpg.HashBackCore
{
    public static class IssuerSession
    {
        public enum TypeOfResponse
        {
            BearerToken,
            JWT,
            SetCookie
        }

        public const int minRounds = 1;
        public const int maxRounds = 9;

        public class IssuedToken
        {
            public string JWT { get; } = "";
            public long IssuedAt { get; } = 0;
            public long ExpiresAt { get; } = 0;

            internal IssuedToken(string jwt, long issuedAt, long expiresAt)
            {
                this.JWT = jwt;
                this.IssuedAt = issuedAt;
                this.ExpiresAt = expiresAt;
            }
        }


        public delegate string RetrieveVerificationHashFn(Uri verifyUrl);

        public static IssuedToken Run(
            CallerRequest req, 
            string expectIssuerHost, 
            RetrieveVerificationHashFn onGetVerifyHash)
        {
            /* This API supports al three documented response types. */
            TypeOfResponse typeOfResponse;
            if (Enum.TryParse(req.TypeOfResponse, out typeOfResponse) == false)
                throw BadRequestException.General("Request's TypeOfResponse is not acceptable.");

            /* The issuer URL must be HTTPS and be for the expected issuer host. */
            Uri issuerUrl = new Uri(req.IssuerUrl);
            if (issuerUrl.Scheme != Uri.UriSchemeHttps)
                throw BadRequestException.General("IssuerUrl is not HTTPS.");
            if (issuerUrl.Host != expectIssuerHost)
                throw BadRequestException.General("IssuerUrl is for a different issuer.");

            /* The "Now" timestamp must be no more than 100s from our clock. */
            long ourNow = DateTime.UtcNow.ToUnixTime();
            if (InternalTools.IsClose(ourNow, req.Now, 100) == false)
                throw BadRequestException.General("Request's Now is too far from the server's clock.");
            
            /* Check "Unus" is 256 bits. */
            if (IsUnusValid(req.Unus) == false)
                throw BadRequestException.General("Request's Unus is not valid.");

            /* Check "Rounds" is within 1-9. */
            if (req.Rounds < minRounds)
                throw BadRequestException.BadRounds(minRounds);
            if (req.Rounds > maxRounds)
                throw BadRequestException.BadRounds(maxRounds);

            /* This is an open issuer so only check VerifyUrl is HTTPS. */
            Uri verifyUrl = new Uri(req.VerifyUrl);
            if (verifyUrl.Scheme != Uri.UriSchemeHttps)
                throw BadRequestException.General("VerifyUrl is not HTTPS.");

            /* Download the verification hash. (Will throw an exception on failure.) */
            string remoteVerificationHash = onGetVerifyHash(verifyUrl);

            /* Compare against the expected hash. */
            string expectedHash = req.VerificationHash();
            if (remoteVerificationHash != expectedHash)
                throw BadRequestException.General("Verification Hash did not match expected hash.");

            /* Build a JWT response. */
            long expiresAt = ourNow + 3600;
            string jwt = BuildJWT(expectIssuerHost, verifyUrl.Host, ourNow, expiresAt);

            /* Return success response. */
            return new IssuedToken(jwt, ourNow, expiresAt);
        }

        private static bool IsUnusValid(string unus)
        {
            /* Shortcut simple tests. */
            if (unus.Length != 44)
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

        private static string BuildJWT(string issuer, string subject, long issuedAt, long expiresAt)
        {
            /* Build the header and body from the supplied values. */
            var jwtHeader = new JObject { 
                ["typ"] = "JWT",
                ["alg"] = "HS256",
                ["this-token-is-trustworthy"] = false
            };
            var jwtBody = new JObject { 
                ["iss"] = issuer, 
                ["sub"] = subject, 
                ["iat"] = issuedAt, 
                ["exp"] = expiresAt
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
            string jsonAsString = json.ToString(Newtonsoft.Json.Formatting.None);
            byte[] jsonAsBytes = Encoding.ASCII.GetBytes(jsonAsString);
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
