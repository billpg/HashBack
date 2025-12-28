using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Represents a policy for validating and parsing HashBack Authorization headers.
    /// </summary>
    public class HashBackValidator
    {
        public delegate bool OnHostValidateDelegate(string hostSupplied);
        public delegate bool OnNowValidateDelegate(long nowSupplied);
        public delegate Task<string?> OnIdentifyUserDelegate(string verifyUr);
        public delegate Task<string> OnGetHashDelegate(string verifyUrl);
        public delegate void OnLogWriteDelegate(string logText);

        public OnHostValidateDelegate OnHostValidate { get; set; }
            = _ => throw new ApplicationException($"{nameof(OnHostValidate)} not implemented.");

        public OnNowValidateDelegate OnNowValidate { get; set; }
            = _ => throw new ApplicationException($"{nameof(OnNowValidate)} not implemented.");

        public OnIdentifyUserDelegate OnIdentifyUser { get; set; }
            = _ => throw new ApplicationException($"{nameof(OnIdentifyUser)} not implemented.");

        public OnGetHashDelegate OnGetHash { get; set; }
            = _ => throw new ApplicationException($"{nameof(OnGetHash)} not implemented.");

        public OnLogWriteDelegate OnLogWrite { get; set; }
            = Helpers.DefaultLogWrite;

        public async Task<string> Validate(string authHeader)
        {
            /* Announce start of Validate for log. */
            OnLogWrite($"Start Validate({authHeader.Length} characters)");

            /* Attempt to decode from base-64. */
            byte[]? jsonBytes = Helpers.TryBase64Decode(authHeader);
            string json;
            if (jsonBytes != null)
            {
                /* Successfully decoded base-64. Convert to string. */
                json = Encoding.UTF8.GetString(jsonBytes);
            }
            /* Could this be an unencoded JSON string instead? */
            else if (MightBeJsonHeader(authHeader))
            {
                /* Use it directly. The JSON-Validate farther down will reject if not.
                 * (We will still need bytes for hashing later so save those.) */
                json = authHeader;
                jsonBytes = Encoding.UTF8.GetBytes(authHeader);
            }
            /* Complain that it's neither valid base-64 nor JSON. */
            else
            {
                throw new AuthorizationParseException(
                    "Authorization header is not valid BASE-64.",
                    ValidateRejectionReason.BadHeader);
            }

            /* Attempt to parse JSON, complaining if it rejects the string. */
            JObject? obj = Helpers.TryJsonParse(json);
            if (obj == null)
            {
                /* If we got here, it means the input was not valid JSON. */
                throw new AuthorizationParseException(
                    "Authorization header is not valid JSON.",
                    ValidateRejectionReason.BadHeader);
            }

            /* Validate Version. */
            string? version = obj["Version"]?.Value<string>();
            if (version == null)
                throw new AuthorizationParseException(
                    "Version property is missing.",
                    ValidateRejectionReason.BadHeader);
            if (!Helpers.IsRecognizedVersion(version))
                throw new AuthorizationParseException(
                    $"Version must be one of {Helpers.SupportedVersions.Select(v => $"'{v}'").ToStringJoin("/")}.", 
                    ValidateRejectionReason.BadHeader);

            /* Validate Host. */
            string? host = obj["Host"]?.Value<string>();
            if (host == null)
                throw new AuthorizationParseException(
                    "Host property is missing.", 
                    ValidateRejectionReason.BadHeader);
            OnLogWrite($"OnHostValidate(\"{host}\")");
            if (!OnHostValidate(host))
                throw new AuthorizationParseException(
                    "Host property is not valid for this server.", 
                    ValidateRejectionReason.WrongHost);

            /* Validate Now. */
            long? now = obj["Now"]?.Value<long>();
            if (now == null)
                throw new AuthorizationParseException(
                    "Now property is missing.", 
                    ValidateRejectionReason.BadHeader);
            OnLogWrite($"OnNowValidate(\"{now.Value}\")");
            if (!OnNowValidate(now.Value))
                throw new AuthorizationParseException(
                    "Now property is not valid for this server's time policy.",
                    ValidateRejectionReason.WrongNow);

            /* Validate Unus. */
            string? unus = obj["Unus"]?.Value<string>();
            if (unus == null)
                throw new AuthorizationParseException(
                    "Unus property is missing.", 
                    ValidateRejectionReason.BadHeader);
            var unusAsBytes = Helpers.TryBase64Decode(unus);
            if (unusAsBytes == null)
                throw new AuthorizationParseException(
                    "Unus property is not base-64.",
                    ValidateRejectionReason.BadHeader);
            if (unusAsBytes.Length < 128/8)
                throw new AuthorizationParseException(
                    "Unus property is not 128 bits.",
                    ValidateRejectionReason.BadHeader);

            /* Validate Verify (must be a valid https URL) */
            string? verifyUrl = obj["Verify"]?.Value<string>();
            if (verifyUrl == null)
                throw new AuthorizationParseException(
                    "Verify property is missing.",
                    ValidateRejectionReason.BadHeader);
            if (!Uri.TryCreate(verifyUrl, UriKind.Absolute, out var uri))
                throw new AuthorizationParseException(
                    "Verify property must be a valid URL.",
                    ValidateRejectionReason.BadHeader);
            if (uri.Scheme != Uri.UriSchemeHttps)
                throw new AuthorizationParseException(
                    "Verify URL must be HTTPS.",
                    ValidateRejectionReason.BadHeader);

            /* Identify the user this verification URL corresponds to. */
            OnLogWrite($"OnIdentifyUser(\"{verifyUrl}\")");
            string? user = await OnIdentifyUser(verifyUrl);
            if (user == null)
                throw new AuthorizationParseException(
                    "Verify URL could not be matched to a known user.",
                    ValidateRejectionReason.UnknownUser);
            OnLogWrite($"OnIdentifyUser returned \"{user}\".");

            /* If all checks pass, compute the expected 
             * hash from the bytes collected earlier. */
            string expectedHash = Helpers.ComputeVerificationHash(version, jsonBytes);
            OnLogWrite($"Expected Hash: \"{expectedHash}\"");

            /* Call the corresponding verification hash getter function.
             * This may throw an exception, which will fall to the caller. */
            OnLogWrite($"OnGetHash(\"{verifyUrl}\")");
            string verificationHash = await OnGetHash(verifyUrl);
            OnLogWrite($"OnGetHash returned \"{verificationHash}\".");

            /* If the two strings don't match, it isn't valid. */
            if (verificationHash != expectedHash)
                throw new AuthorizationParseException(
                    "The verification hash did not match the expected hash.",
                    ValidateRejectionReason.WrongHash);

            /* If it passed all of the above tests, the header is valid.
             * Return the user returned by the user id function earlier. */
            OnLogWrite("Passed validation.");
            return user;
        }

        /// <summary>
        /// Quick check to see if it looks 
        /// like JSON and contains no whitespace.
        /// </summary>
        /// <param name="authHeader">Header to test.</param>
        /// <returns>True if this might be JSON. False otherwise.</returns>
        private static bool MightBeJsonHeader(string authHeader)
            => authHeader.StartsWith('{') &&
               authHeader.EndsWith('}') &&
               authHeader.Any(char.IsWhiteSpace) == false;
    }
}