using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    public class HashBackBuilder /*
        string? UseHost = null,
        Func<long> NowGetter = null!,
        Func<string> UnusGetter = null!,
        Func<string>? VerifyGetter = null,
        Action<AuthHeaderBuildResult>? PostBuild = null) */
    {
        public delegate long OnGetNowDelegate();
        public delegate string OnGetUnusDelegate();
        public delegate Task<string> OnGetVerifyDelegate();
        public delegate Task OnRegisterHashDelegate(string verifyUrl, string hash);

        public string Host { get; set; } = null!;
        public OnGetNowDelegate NowGetter { get; set; } = DefaultNowGetter;
        public OnGetUnusDelegate UnusGetter { get; set; } = DefaultUnusGenerator;
        public OnGetVerifyDelegate VerifyGetter { get; set; } = null!;
        public OnRegisterHashDelegate? HashRegister { get; set; } = null;

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

        private readonly ConcurrentDictionary<string, string>
            generatedHashes = new ConcurrentDictionary<string, string>();

        public IEnumerable<string> GeneratedHashUrls => generatedHashes.Keys;
        public string GeneratedHash(string verifyUrl) => generatedHashes[verifyUrl];
        public void ClearGeneratedHash(string verifyUrl) => generatedHashes.Remove(verifyUrl, out _);

        public async Task<string> Build()
        {
            if (this.Host == null)
                throw new ApplicationException("Called Build without setting Host property.");
            return await Build(this.Host);
        }

        public async Task<string> Build(string host)
        {
            /* Check the optional properties have all been assigned. */
            if (string.IsNullOrEmpty(this.Host))
                throw new ApplicationException("Host must be set.");
            if (VerifyGetter == null)
                throw new ApplicationException("VerifyUrlGetter must be set.");

            /* Get the Verify URL, which we'll need when building the return object. */
            string verify = await VerifyGetter();

            /* Serialize parameters to JSON and encode. */
            string json = new JObject
            {
                ["Version"] = Helpers.VersionString,
                ["Host"] = host,
                ["Now"] = this.NowGetter(),
                ["Unus"] = this.UnusGetter(),
                ["Verify"] = verify
            }.ToString(Newtonsoft.Json.Formatting.None);
            byte[] jsonAsBytes = Encoding.UTF8.GetBytes(json);
            string authHeader = Convert.ToBase64String(jsonAsBytes);

            /* Compute the verification hash using the Helpers function. */
            string verificationHash = Helpers.ComputeVerificationHash(jsonAsBytes);

            /* Register this verify/hash combo to let it be downloaded. */
            if (this.HashRegister != null)
                await this.HashRegister(verify, verificationHash);
            else
                this.generatedHashes[verify] = verificationHash;

            /* Return the result. */
            return authHeader;
        }
    }
}
