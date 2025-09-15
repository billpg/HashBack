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


    public record AuthHeaderBuilder(
        string? UseHost = null,
        Func<long> NowGetter = null!,
        Func<string> UnusGenerator = null!,
        Func<string>? VerifyGenerator = null,
        Action<AuthHeaderBuildResult>? PostBuild = null)
    {
        public AuthHeaderBuilder() : this(
            UseHost: null,
            NowGetter: DefaultNowGetter,
            UnusGenerator: DefaultUnusGenerator,
            VerifyGenerator: null,
            PostBuild: null) {}

        private static long DefaultNowGetter()
            => DateTime.UtcNow.ToUnixTimeSeconds();

        private static string DefaultUnusGenerator()
        {
            byte[] unusBytes = new byte[16];
            RandomNumberGenerator.Fill(unusBytes);
            return Convert.ToBase64String(unusBytes);
        }

        public AuthHeaderBuilder WithHost(string host)
            => this with { UseHost = host };

        public AuthHeaderBuilder WithNowGetter(Func<long> nowGetter) 
            => this with { NowGetter = nowGetter };

        public AuthHeaderBuilder WithUnusGenerator(Func<string> unusGenerator)   
            => this with { UnusGenerator = unusGenerator };

        public AuthHeaderBuilder WithVerifyGenerator(Func<string> verifyGenerator)
            => this with { VerifyGenerator = verifyGenerator };

        public AuthHeaderBuilder WithVerify(string verify)
            => this with { VerifyGenerator = () => verify };

        public AuthHeaderBuilder WithPostBuild(Action<AuthHeaderBuildResult> postBuild)
            => this with { PostBuild = postBuild };

        public AuthHeaderBuildResult Build()
        {
            /* Check the optional properties have all been assigned. */
            if (UseHost == null)
                throw new InvalidOperationException("UseHost must be set.");
            if (VerifyGenerator == null)
                throw new InvalidOperationException("VerifyUrlGetter must be set.");

            /* Get the Verify URL, which we'll need when building the return object. */
            string verify = VerifyGenerator();

            /* Serialize parameters to JSON and encode. */
            string json = new JObject
            {
                ["Version"] = Helpers.VersionString,
                ["Host"] = this.UseHost,
                ["Now"] = this.NowGetter(),
                ["Unus"] = this.UnusGenerator(),
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

        public string BuildAuthHeader()
            => Build().AuthHeader;

        /// <summary>
        /// Produces the BASE-64 block and verification hash for a HashBack Authorization header,
        /// generating its own Now and Unus values.
        /// </summary>
        /// <param name="host">The full domain name of the server being called.</param>
        /// <param name="verify">The HTTPS URL where the verification hash will be published.</param>
        /// <returns>
        /// An <see cref="AuthHeaderBuildResult"/> containing the BASE-64 encoded JSON block and the verification hash.
        /// </returns>
        public static AuthHeaderBuildResult BuildAuthorization(
            string host,
            string verify)
        {
            var builder = new AuthHeaderBuilder()
            {
                UseHost = host,
                NowGetter = DefaultNowGetter,
                UnusGenerator = DefaultUnusGenerator,
                VerifyGenerator = () => verify
            };
            return builder.Build();
        }

        public static AuthHeaderBuildResult BuildAuthorization(
            string host,
            long now,
            string unus,
            string verify)
        { 
            var builder = new AuthHeaderBuilder()
            {
                UseHost = host,
                NowGetter = () => now,
                UnusGenerator = () => unus,
                VerifyGenerator = () => verify
            };
            return builder.Build();
        }
    }
}
