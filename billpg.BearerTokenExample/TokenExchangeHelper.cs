namespace billpg.BearerTokenExample
{
    /// <summary>
    /// Helper for the cryptographic steps of the PickAName exchange.
    /// </summary>
    public class TokenExchangeHelper
    {
        /// <summary>
        /// Handy copy of UTF8 GetBytes without BOM.
        /// </summary>
        private static readonly Func<string,byte[]> UTF8 
            = new System.Text.UTF8Encoding(false).GetBytes;

        /// <summary>
        /// The name of this exchange.
        /// </summary>
        public const string ExchangeName = "PickAName";

        /// <summary>
        /// The current (only) version of this exchange.
        /// </summary>
        public const string VersionValue = "DRAFTY-DRAFT-2";

        public const string SaltAddedText = "2C266D36-53FB-459D-8B4D-AD67737DA026";

        /// <summary>
        /// Pre-computed byte array of the PBKDF2 salt bytes.
        /// (Used in PBKDF2 during constructor.)
        /// </summary>
        private static readonly IList<byte> saltBytes
            = UTF8(ExchangeName + "/" + VersionValue + "/" + SaltAddedText).ToList().AsReadOnly();

        /// <summary>
        /// The initiator key string.
        /// </summary>
        public string InitiatorKeyText { get; }

        /// <summary>
        /// Pre-computed HMAC key from the initiator key text and fixed salt.
        /// </summary>
        private readonly IList<byte> keyBytes;

        /// <summary>
        /// Construct helper, generating initiator's key if needed. 
        /// </summary>
        /// <param name="initiatorKeyText">Initiator's key, or null to generate a key.</param>
        public TokenExchangeHelper(string? initiatorKeyText = null)
        {
            /* Replace a null value key text wth a random key and store. */
            if (string.IsNullOrEmpty(initiatorKeyText))
            {
                /* Generate 264 random bits, which will base64 to exactly 44 characters. */
                using var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
                byte[] randomBytes = new byte[44 * 6 / 8];
                rnd.GetBytes(randomBytes);

                /* Encode and store bytes as text. Add hyphens so it doesn't get mistaken for base64. */
                var keyText = Convert.ToBase64String(randomBytes);
                for (int insertIndex = 3; insertIndex > 0; insertIndex--)
                    keyText = keyText.Insert(insertIndex * 11, "-");
                this.InitiatorKeyText = keyText;
            }
            else
            {
                /* Store the supplied string unprocessed. */
                this.InitiatorKeyText = initiatorKeyText;
            }

            /* Pre-compute the fixed salt and initiator's key (either as
             * supplied or generated) and store for use in HMAC calls later. */
            this.keyBytes = 
                System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                    UTF8(this.InitiatorKeyText), 
                    saltBytes.ToArray(), 
                    10,
                    System.Security.Cryptography.HashAlgorithmName.SHA256,
                    256 / 8
                )
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Evidence of knowing initiator key text for the initiator.
        /// </summary>
        public string VerifyEvidenceForInitiator => CalcVerifyHash(1);

        /// <summary>
        /// Evidence of knowing initiator's key text for the issuer.
        /// </summary>
        public string VerifyEvidenceForIssuer => CalcVerifyHash(2);

        /// <summary>
        /// Calculate the HMAC or a single byte using ore-computerd HMAC key 
        /// to provide evidence of knowing the initiator's key text.
        /// </summary>
        /// <param name="roleByte">1 for initiator. 2 for issuer.</param>
        /// <returns>HMAC result in hex.</returns>
        private string CalcVerifyHash(byte roleByte) => HMAC(new byte[] { roleByte });
        
        /// <summary>
        /// Sign a bearer token uisng the initiator's HMAC key.
        /// </summary>
        /// <param name="bearerToken">Token to signed.</param>
        /// <returns>Signature for Bearer token using initiator';s key text.</returns>
        public string SignBearerToken(string bearerToken) => HMAC(UTF8(bearerToken));        

        /// <summary>
        /// Perfoem a HMAC operation using the pre-computed HMAC key and supplied input bytes.
        /// </summary>
        /// <param name="input">Input bytes to sign with initiator's pre-computed HMAC key.</param>
        /// <returns>Hex-encoded HMAC result.</returns>
        private string HMAC(byte[] input)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(this.keyBytes.ToArray());
            return BytesToHex(hmac.ComputeHash(input));
        }

        /// <summary>
        /// Unitity function to convert bytes into a hex-encoded string.
        /// </summary>
        /// <param name="bytes">Bytes to encode.</param>
        /// <returns>Hex encoded bytes.</returns>
        private static string BytesToHex(IList<byte> bytes)
            => string.Concat(bytes.Select(b => b.ToString("X2")));        
    }
}
