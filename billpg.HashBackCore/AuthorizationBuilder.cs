using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    public readonly struct AuthorizationBuildResult
    {
        public string AuthHeader { get; }
        public string VerifyUrl { get; }
        public string VerificationHash { get; }
        public AuthorizationBuildResult(string authHeader, string verifyUrl, string verificationHash)
        {
            AuthHeader = authHeader;
            VerifyUrl = verifyUrl;
            VerificationHash = verificationHash;
        }
    }


    public record AuthorizationBuilder(
        string? UseHost = null,
        Func<long> NowGetter = null!,
        Func<string> UnusGenerator = null!,
        Func<string>? VerifyGenerator = null,
        Action<AuthorizationBuildResult>? PostBuild = null)
    {
        public AuthorizationBuilder() : this(
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

        public AuthorizationBuilder WithHost(string host)
            => this with { UseHost = host };

        public AuthorizationBuilder WithNowGetter(Func<long> nowGetter) 
            => this with { NowGetter = nowGetter };

        public AuthorizationBuilder WithUnusGenerator(Func<string> unusGenerator)   
            => this with { UnusGenerator = unusGenerator };

        public AuthorizationBuilder WithVerifyGenerator(Func<string> verifyGenerator)
            => this with { VerifyGenerator = verifyGenerator };

        public AuthorizationBuilder WithVerify(string verify)
            => this with { VerifyGenerator = () => verify };

        public AuthorizationBuilder WithPostBuild(Action<AuthorizationBuildResult> postBuild)
            => this with { PostBuild = postBuild };

        public AuthorizationBuildResult Build()
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
            var result = new AuthorizationBuildResult(authHeader, verify, verificationHash);

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
        /// An <see cref="AuthorizationBuildResult"/> containing the BASE-64 encoded JSON block and the verification hash.
        /// </returns>
        public static AuthorizationBuildResult BuildAuthorization(
            string host,
            string verify)
        {
            var builder = new AuthorizationBuilder()
            {
                UseHost = host,
                NowGetter = DefaultNowGetter,
                UnusGenerator = DefaultUnusGenerator,
                VerifyGenerator = () => verify
            };
            return builder.Build();
        }

        public static AuthorizationBuildResult BuildAuthorization(
            string host,
            long now,
            string unus,
            string verify)
        { 
            var builder = new AuthorizationBuilder()
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
