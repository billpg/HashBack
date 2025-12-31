using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace billpg.HashBackCore
{
    public class HashBackBuilder
    {
        public delegate long OnGetNowDelegate();
        public delegate string OnGetUnusDelegate();
        public delegate Task<string> OnGetVerifyDelegate();
        public delegate Task OnRegisterHashDelegate(string verifyUrl, string hash);

        public string Host { get; set; } = null!;
        public OnGetNowDelegate NowGetter { get; set; } = DefaultNowGetter;
        public OnGetUnusDelegate UnusGetter { get; set; } = DefaultUnusGenerator;
        public OnGetVerifyDelegate VerifyGetter { get; set; } = null!;
        public OnRegisterHashDelegate HashRegister { get; set; } = null!;

        public void SetSyncVerifyGetter(Func<string> syncVerifyGetter)
            => this.VerifyGetter = () => Task.FromResult(syncVerifyGetter());

        public void SetVerify(string verify)
            => this.SetSyncVerifyGetter(() => verify);

        public void SetSyncHashRegister(Action<string, string> hashRegister)
            => this.HashRegister = (url, hash) => Task.Run(() => hashRegister(url, hash));
        
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

        public async Task<string> Build()
        {
            /* Check the required properties have all been assigned. */
            if (string.IsNullOrEmpty(this.Host))
                throw new ApplicationException("Host must be set.");
            if (this.VerifyGetter == null)
                throw new ApplicationException("VerifyGetter must be set.");
            if (this.HashRegister == null)
                throw new ApplicationException("HashRegister must be set.");

            /* Get the verify URL once. */
            string verify = await this.VerifyGetter();

            /* Call through to the build function. */
            (string authHeader, string verificationHash) 
                = Helpers.Build(
                    this.Host,
                    this.NowGetter(),
                    this.UnusGetter(),
                    verify);

            /* Register this verify/hash combo to let it be downloaded. */
            await this.HashRegister(verify, verificationHash);

            /* Return the result. */
            return authHeader;
        }
    }
}
