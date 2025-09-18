using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Represents a policy for validating and parsing HashBack Authorization headers.
    /// </summary>
    public record AuthHeaderValidator(
        Func<string, bool> HostTest = null!,
        Func<long, bool> NowTest = null!,
        Func<string, Task<string>> UserIdentifier = null!,
        Func<string, Task<string>> VerificationHashGetter = null!)
    {
        /// <summary>
        /// Initializes a new AuthHeaderValidator with default 
        /// host and time tests that throw exceptions.
        /// </summary>
        public AuthHeaderValidator() : this(
            HostTest: _ => throw new Exception(),
            NowTest: _ => throw new Exception(),
            UserIdentifier: _ => throw new Exception(),
            VerificationHashGetter: _ => throw new Exception())
        { }

        /// <summary>
        /// Parses and validates an Authorization header according to this policy.
        /// Throws custom exception if the header is invalid.
        /// (Note that the verification URL will need to be linked to a user and the
        /// verification hash itself will need to be compared separately.)
        /// </summary>
        /// <param name="authHeader">The Authorization header block in JSON or BASE-64 encoded JSON.</param>
        /// <returns>The verification URL and expected hash.</returns>
        /// <exception cref="AuthorizationParseException">
        /// Thrown if the header is invalid or fails policy checks.
        /// </exception>
        public async Task<string> Validate(string authHeader)
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
                /* Use it directly. The JSON-Validate farther down will reject if not.
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

            /* Validate Unus. */
            string? unus = obj["Unus"]?.Value<string>();
            if (unus == null)
                throw new AuthorizationParseException("Unus property is missing.");
            var unusAsBytes = Helpers.TryBase64Decode(unus);
            if (unusAsBytes == null)
                throw new AuthorizationParseException("Unus property is not base-64.");
            if (unusAsBytes.Length < 128/8)
                throw new AuthorizationParseException("Unus property is not 128 bits.");

            /* Validate Verify (must be a valid https URL) */
            string? verifyUrl = obj["Verify"]?.Value<string>();
            if (verifyUrl == null)
                throw new AuthorizationParseException("Verify property is missing.");
            if (!Uri.TryCreate(verifyUrl, UriKind.Absolute, out var uri))
                throw new AuthorizationParseException("Verify property must be a valid URL.");
            if (uri.Scheme != Uri.UriSchemeHttps)
                throw new AuthorizationParseException("Verify URL must be HTTPS.");

            /* Identify the user this verification URL corresponds to. */
            string? user = await UserIdentifier(verifyUrl);
            if (user == null)
                throw new AuthorizationParseException(
                    "Verify URL could not be matched to a known user.");

            /* If all checks pass, compute the expected 
             * hash from the bytes collected earlier. */
            string expectedHash = Helpers.ComputeVerificationHash(jsonBytes);

            /* Call the corresponding verification hash getter function.
             * This may throw an exception, which will fall to the caller. */
            string verificationHash = await VerificationHashGetter(verifyUrl);

            /* If the two strings don't match, it isn't valid. */
            if (verificationHash != expectedHash)
                throw new AuthorizationParseException(
                    "The verification hash did not match the expected hash.");

            /* If it passed all of the above tests, the header is valid.
             * Return the user returned by the user id function earlier. */
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

        /// <summary>
        /// Returns a new <see cref="AuthHeaderParser"/> with the specified host validation function.
        /// </summary>
        /// <param name="hostTest">A function that returns true if the Host property is valid.</param>
        /// <returns>A new <see cref="AuthHeaderParser"/> with the updated host test.</returns>
        public AuthHeaderValidator WithHostTest(Func<string, bool> hostTest)
            => this with { HostTest = hostTest };

        /// <summary>
        /// Returns a new <see cref="AuthHeaderParser"/> with the specified time validation function.
        /// </summary>
        /// <param name="nowTest">A function that returns true if the Now property (seconds since epoch) is valid.</param>
        /// <returns>A new <see cref="AuthHeaderParser"/> with the updated time test.</returns>
        public AuthHeaderValidator WithNowTest(Func<long, bool> nowTest)
            => this with { NowTest = nowTest };

        public AuthHeaderValidator WithUserIdentifier(Func<string, Task<string>> userIdentifier)
            => this with { UserIdentifier = userIdentifier };

        public AuthHeaderValidator WithVerificationHashGetter(Func<string, Task<string>> verificationHashGetter)
            => this with { VerificationHashGetter = verificationHashGetter };
    }
}