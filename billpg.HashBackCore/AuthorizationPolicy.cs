using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Represents a policy for validating and parsing HashBack Authorization headers.
    /// </summary>
    public record AuthorizationPolicy(
        Func<string, bool> HostTest = null!,
        Func<long, bool> NowTest = null!)
    {
        /// <summary>
        /// Initializes a new AuthorizationPolicy with default host and time tests (both always fail).
        /// </summary>
        public AuthorizationPolicy() : this(_ => false, _ => false) { }

        /// <summary>
        /// Parses and validates a BASE-64 encoded Authorization header according to this policy.
        /// Throws <see cref="AuthorizationParseException"/> if the header is invalid.
        /// </summary>
        /// <param name="authHeader">The BASE-64 encoded Authorization header block.</param>
        /// <returns>
        /// An <see cref="AuthorizationParseResult"/> containing the verification URL and expected hash.
        /// </returns>
        /// <exception cref="AuthorizationParseException">Thrown if the header is invalid or fails policy checks.</exception>
        public AuthorizationParseResult Parse(string authHeader)
        {
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
                /* Use it directly. The JSON-Parse farther down will reject if not.
                 * (We will still need bytes for hashing later so save those.) */
                json = authHeader;
                jsonBytes = Encoding.UTF8.GetBytes(authHeader);
            }
            /* Complain that it's neither valid base-64 nor JSON. */
            else
            {
                throw new AuthorizationParseException("Authorization header is not valid BASE-64.");
            }

            /* Attempt to parse JSON, complaining if it rejects the string. */
            JObject? obj = Helpers.TryJsonParse(json);
            if (obj == null)
            {
                /* If we got here, it means the input was not valid JSON. */
                throw new AuthorizationParseException("Authorization header is not valid JSON.");
            }

            /* Validate Version. */
            string? version = obj["Version"]?.Value<string>();
            if (version == null)
                throw new AuthorizationParseException("Version property is missing.");
            if (version != Helpers.VersionString)
                throw new AuthorizationParseException($"Version must be '{Helpers.VersionString}'.");

            /* Validate Host. */
            string? host = obj["Host"]?.Value<string>();
            if (host == null)
                throw new AuthorizationParseException("Host property is missing.");
            if (!HostTest(host))
                throw new AuthorizationParseException("Host property is not valid for this server.");

            /* Validate Now. */
            long? now = obj["Now"]?.Value<long>();
            if (now == null)
                throw new AuthorizationParseException("Now property is missing.");
            if (!NowTest(now.Value))
                throw new AuthorizationParseException("Now property is not valid for this server's time policy.");

            /* Validate Verify (must be a valid https URL) */
            string? verifyUrl = obj["Verify"]?.Value<string>();
            if (verifyUrl == null)
                throw new AuthorizationParseException("Verify property is missing.");
            if (!Uri.TryCreate(verifyUrl, UriKind.Absolute, out var uri))
                throw new AuthorizationParseException("Verify property must be a valid URL.");
            if (uri.Scheme != Uri.UriSchemeHttps)
                throw new AuthorizationParseException("Verify URL must be HTTPS.");

            /* If all checks pass, compute the expected hash from 
             * the bytes collected earlier and return both. */
            string expectedHash = Helpers.ComputeVerificationHash(jsonBytes);
            return new AuthorizationParseResult(verifyUrl, expectedHash);
        }

        private static bool MightBeJsonHeader(string authHeader)
        {
            /* Quick check to see if it looks like JSON and
             * contains no whitespace. */
            return 
                authHeader.StartsWith("{") && 
                authHeader.EndsWith("}") 
                && authHeader.Any(char.IsWhiteSpace) == false;
        }

        /// <summary>
        /// Returns a new <see cref="AuthorizationPolicy"/> with the specified host validation function.
        /// </summary>
        /// <param name="hostTest">A function that returns true if the Host property is valid.</param>
        /// <returns>A new <see cref="AuthorizationPolicy"/> with the updated host test.</returns>
        public AuthorizationPolicy WithHostTest(Func<string, bool> hostTest)
            => this with { HostTest = hostTest };

        /// <summary>
        /// Returns a new <see cref="AuthorizationPolicy"/> with the specified time validation function.
        /// </summary>
        /// <param name="nowTest">A function that returns true if the Now property (seconds since epoch) is valid.</param>
        /// <returns>A new <see cref="AuthorizationPolicy"/> with the updated time test.</returns>
        public AuthorizationPolicy WithNowTest(Func<long, bool> nowTest)
            => this with { NowTest = nowTest };
    }
}