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
        /// <param name="authHeaderBase64">The BASE-64 encoded Authorization header block.</param>
        /// <returns>
        /// An <see cref="AuthorizationParseResult"/> containing the verification URL and expected hash.
        /// </returns>
        /// <exception cref="AuthorizationParseException">Thrown if the header is invalid or fails policy checks.</exception>
        public AuthorizationParseResult Parse(string authHeaderBase64)
        {
            byte[] jsonBytes;
            try
            {
                jsonBytes = Convert.FromBase64String(authHeaderBase64);
            }
            catch (FormatException)
            {
                throw new AuthorizationParseException("Authorization header is not valid BASE-64.");
            }

            string json = Encoding.UTF8.GetString(jsonBytes);

            JObject? obj;
            try
            {
                obj = JObject.Parse(json);
            }
            catch (JsonReaderException)
            {
                throw new AuthorizationParseException("Authorization header is not valid JSON.");
            }

            // Validate Version
            string? version = obj["Version"]?.Value<string>();
            if (version == null)
                throw new AuthorizationParseException("Version property is missing.");
            if (version != Helpers.VersionString)
                throw new AuthorizationParseException($"Version must be '{Helpers.VersionString}'.");

            // Validate Host
            string? host = obj["Host"]?.Value<string>();
            if (host == null)
                throw new AuthorizationParseException("Host property is missing.");
            if (!HostTest(host))
                throw new AuthorizationParseException("Host property is not valid for this server.");

            // Validate Now
            long? now = obj["Now"]?.Value<long>();
            if (now == null)
                throw new AuthorizationParseException("Now property is missing.");
            if (!NowTest(now.Value))
                throw new AuthorizationParseException("Now property is not valid for this server's time policy.");

            // Validate Verify (must be a valid https URL)
            string? verifyUrl = obj["Verify"]?.Value<string>();
            if (verifyUrl == null)
                throw new AuthorizationParseException("Verify property is missing.");
            if (!Uri.TryCreate(verifyUrl, UriKind.Absolute, out var uri))
                throw new AuthorizationParseException("Verify property must be a valid URL.");
            if (uri.Scheme != Uri.UriSchemeHttps)
                throw new AuthorizationParseException("Verify URL must be HTTPS.");

            // If all checks pass, compute the expected hash and return.
            string expectedHash = Helpers.ComputeVerificationHash(jsonBytes);
            return new AuthorizationParseResult(verifyUrl, expectedHash);
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