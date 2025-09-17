using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    /// <summary>
    /// Result of building an Authorization header.
    /// </summary>
    public readonly struct AuthHeaderBuildResult
    {
        /// <summary>
        /// The generated BASE-64 encoded JSON block for the Authorization header.
        /// </summary>
        public string AuthHeader { get; }

        /// <summary>
        /// The Verify URL that was included in the JSON block.
        /// </summary>
        public string VerifyUrl { get; }

        /// <summary>
        /// The computed verification hash for the JSON block.
        /// </summary>
        public string VerificationHash { get; }

        public AuthHeaderBuildResult(string authHeader, string verifyUrl, string verificationHash)
        {
            AuthHeader = authHeader;
            VerifyUrl = verifyUrl;
            VerificationHash = verificationHash;
        }
    }

    /// <summary>
    /// An object for building a HashBack Authorization header.
    /// </summary>
    /// <param name="UseHost">Use this string as the JSON's Host value.</param>
    /// <param name="NowGetter">Callable to return the current time.</param>
    /// <param name="UnusGetter">Callable to return an Unus header.</param>
    /// <param name="VerifyGetter">Callable to return a Verify URL string.</param>
    /// <param name="PostBuild">Called after header and hash have been generated.</param>
    public record AuthHeaderBuilder(
        string? UseHost = null,
        Func<long> NowGetter = null!,
        Func<string> UnusGetter = null!,
        Func<string>? VerifyGetter = null,
        Action<AuthHeaderBuildResult>? PostBuild = null)
    {
        /// <summary>
        /// Builds an AuthHeaderBuilder with default Now and Unus generators.
        /// </summary>
        public AuthHeaderBuilder() : this(
            UseHost: null,
            NowGetter: DefaultNowGetter,
            UnusGetter: DefaultUnusGenerator,
            VerifyGetter: null,
            PostBuild: null) {}

        /// <summary>
        /// Builds an AuthHeaderBuilder with supplied Host 
        /// and Verify strings and default Now and Unus generators.
        /// </summary>
        /// <param name="host">String to use as Host value.</param>
        /// <param name="verify">String to use as Verify value.</param>
        public AuthHeaderBuilder(string host, string verify) : this(
            UseHost: host,
            NowGetter: DefaultNowGetter,
            UnusGetter: DefaultUnusGenerator,
            VerifyGetter: () => verify,
            PostBuild: null) {}

        /// <summary>
        /// Funtion to use as the default Now generator, returning
        /// the current time in Unix time seconds. Intended to be
        /// used as the default for NowGetter rather than being
        /// called directly.
        /// </summary>
        /// <returns>Current time in 1970 format.</returns>
        private static long DefaultNowGetter()
            => DateTime.UtcNow.ToUnixTimeSeconds();

        /// <summary>
        /// Fuction to use as the default Unus generator, returning
        /// a random 16-byte value encoded as BASE-64. Intended
        /// to be used as the default for UnusGetter rather
        /// than being called directly.
        /// </summary>
        /// <returns>New Unus string.</returns>
        private static string DefaultUnusGenerator()
        {
            /* Generate 16 cryptographic-quality 
             * random bytes and encode as BASE-64. */
            byte[] unusBytes = new byte[16];
            RandomNumberGenerator.Fill(unusBytes);
            return Convert.ToBase64String(unusBytes);
        }

        /// <summary>
        /// Returns a copy of this builder with the specified Host value.
        /// </summary>
        /// <param name="host">String to use as the Host value.</param>
        /// <returns>New object with updated property.</returns>
        public AuthHeaderBuilder WithHost(string host)
            => this with { UseHost = host };

        /// <summary>
        /// Returns a copy of this builder with the specified Now generator.
        /// </summary>
        /// <param name="nowGetter">New Now-getter.</param>
        /// <returns>New object with updated property.</returns>
        public AuthHeaderBuilder WithNowGetter(Func<long> nowGetter) 
            => this with { NowGetter = nowGetter };

        public AuthHeaderBuilder WithNowGetter(Func<DateTime> nowGetter)
            => WithNowGetter(() => nowGetter().ToUnixTimeSeconds());

        public AuthHeaderBuilder WithNow(DateTime now)
            => WithNow(now.ToUnixTimeSeconds());

        public AuthHeaderBuilder WithNow(long now)
            => WithNowGetter(() => now);

        /// <summary>
        /// Returns a copy of this builder with the specified Unus getter.
        /// </summary>
        /// <param name="unusGetter">New Unus-Getter.</param>
        /// <returns>New object with updated property.</returns>
        public AuthHeaderBuilder WithUnusGetter(Func<string> unusGetter)   
            => this with { UnusGetter = unusGetter };

        public AuthHeaderBuilder WithUnus(string unus)
            => WithUnusGetter(() => unus);

        public AuthHeaderBuilder WithVerifyGetter(Func<string> verifyGetter)
            => this with { VerifyGetter = verifyGetter };

        public AuthHeaderBuilder WithVerify(string verify)
            => this with { VerifyGetter = () => verify };

        public AuthHeaderBuilder WithPostBuild(Action<AuthHeaderBuildResult> postBuild)
            => this with { PostBuild = postBuild };

        public AuthHeaderBuildResult Build()
        {
            /* Check the optional properties have all been assigned. */
            if (UseHost == null)
                throw new InvalidOperationException("UseHost must be set.");
            if (VerifyGetter == null)
                throw new InvalidOperationException("VerifyUrlGetter must be set.");

            /* Get the Verify URL, which we'll need when building the return object. */
            string verify = VerifyGetter();

            /* Serialize parameters to JSON and encode. */
            string json = new JObject
            {
                ["Version"] = Helpers.VersionString,
                ["Host"] = this.UseHost,
                ["Now"] = this.NowGetter(),
                ["Unus"] = this.UnusGetter(),
                ["Verify"] = verify
            }.ToString(Newtonsoft.Json.Formatting.None);
            byte[] jsonAsBytes = Encoding.UTF8.GetBytes(json);
            string authHeader = Convert.ToBase64String(jsonAsBytes);

            /* Compute the verification hash using the Helpers function. */
            string verificationHash = Helpers.ComputeVerificationHash(jsonAsBytes);
            var result = new AuthHeaderBuildResult(authHeader, verify, verificationHash);

            /* Pass it to the PostBuild callable if we got one. */
            PostBuild?.Invoke(result);

            /* Return the result. */
            return result;
        }

        /// <summary>
        /// Build an Authorization header string only.
        /// (Intended for use when a separate PostBuild
        /// will have captured the verifiation hash.)
        /// </summary>
        /// <returns>Authorization header in BASE-64.</returns>
        public string BuildAuthHeader()
            => Build().AuthHeader;

        /// <summary>
        /// Convenience function to build an Authorization header and
        /// compute the verification hash in one call.
        /// </summary>
        /// <param name="host">Name of host to use in request.</param>
        /// <param name="verify">Location of verification URL.</param>
        /// <returns>The authorization header and verification hash.</returns>
        public static AuthHeaderBuildResult Build(string host, string verify)
            => new AuthHeaderBuilder(host, verify).Build();
    }
}
