using Newtonsoft.Json.Linq;

namespace billpg.CrossRequestTokenExchange
{
    /// <summary>
    /// Tools for assisting an Initiator conduct an exchange.
    /// </summary>
    public class InitiatorHelper
    {
        /// <summary>
        /// For the caller to implement, providing everything the Initiator needs.
        /// </summary>
        public interface IExchangeHandler
        {
            /// <summary>
            /// Called when the handler needs to store a newly opened exchange.
            /// </summary>
            /// <param name="exchangeId">Generated exchangeId.</param>
            /// <param name="requestBody">Request body to store.</param>
            void StoreInitiateRequest(Guid exchangeId, JObject requestBody);

            /// <summary>
            /// Called when the handler needs to retrieve a past initiate exchange.
            /// </summary>
            /// <param name="exchangeId">Prior request's exhangeId.</param>
            /// <returns>JSON data peviously passed to StoreInitiateRequest,
            /// or NULL for no-such-exchange.</returns>
            JObject? RetrieveInitiateRequest(Guid exchangeId);

            /// <summary>
            /// Called when the exchange is completed and we have a validated token.
            /// </summary>
            /// <param name="bearerToken">The Bearer token issed by the issuer.</param>
            /// <param name="expiresAt">When this token will expire.</param>
            void OnValidatedToken(string bearerToken, DateTime expiresAt);
        }

        /// <summary>
        /// Gets the configured exchnage handler for this helper.
        /// </summary>
        public IExchangeHandler ExchangeHandler { get; }

        /// <summary>
        /// Construct a new initiator handler object.
        /// </summary>
        /// <param name="exchangeHandler">Configured Exchnage Handler object.</param>
        public InitiatorHelper(IExchangeHandler exchangeHandler)
        {
            this.ExchangeHandler = exchangeHandler;
        }

        /// <summary>
        /// Generate a suitable Initiate request JSON suitable for sending to the
        /// initiator.
        /// </summary>
        /// <returns>JSON request object.</returns>
        public JObject GenerateInitiateRequest()
        {
            /* Select an exchange ID. */
            var exchangeId = Guid.NewGuid();

            /* Populate the request JSON object. */
            var requestJson = new JObject
            {
                ["CrossRequestTokenExchange"] = "DRAFTY-DRAFT-3",
                ["ExchangeId"] = exchangeId.ToString().ToUpperInvariant(),
                ["InitiatorsKey"] = CryptoHelpers.GenerateRandomKeyString()
            };

            /* Pass the generated object ono the exchange handler. */
            ExchangeHandler.StoreInitiateRequest(exchangeId, requestJson);

            /* Return the completed object to the caller. */
            return requestJson;
        }

        /// <summary>
        /// Handle an Issue request made by the issuer. If valid, returns null and 
        /// the validated Bearer token will be passed to the exchange handler.
        /// If not valid, returns error message to use for a 400 (BadRequest) response.
        /// </summary>
        /// <param name="issueRequestJson">Parsed Issue request JSON.</param>
        /// <returns>NULL for success or error message.</returns>
        public string? HandleIssueRequest(JObject issueRequestJson)
        {
            /* Pull out the exchangeId of the Issue request. */
            string? exchangeIdAsString = issueRequestJson["ExchangeId"]?.Value<string>();
            if (string.IsNullOrEmpty(exchangeIdAsString))
                return "Missing/Empty ExchangeId property.";
            if (Guid.TryParse(exchangeIdAsString, out Guid exchangeId) == false)
                return "ExchangeId property is not a GUID.";

            /* Fetch the stored initiate message for this exchangeId. */
            JObject? initiateRequestJson = ExchangeHandler.RetrieveInitiateRequest(exchangeId);
            if (initiateRequestJson == null)
                return "Can't find an open exchange with this ExchangeId.";

            /* Exract and validate the issuer's key. */
            string? issuersKey = issueRequestJson["IssuersKey"]?.Value<string>();
            if (issuersKey == null)
                return "IssuersKey is missing.";
            string? validateIssuersKeyMessage = TextHelpers.ValidateKey(issuersKey, "IssuersKey", 0);
            if (validateIssuersKeyMessage != null)
                return validateIssuersKeyMessage;

            /* Extract and validate the initiator's key. */
            string? initiatorsKey = initiateRequestJson["InitiatorsKey"]?.Value<string>();
            if (initiatorsKey == null)
                return "InitiatorsKey is missing.";
            string? validateInitiatorsKeyMessage = TextHelpers.ValidateKey(initiatorsKey, "InitiatorsKey", 33);
            if (validateInitiatorsKeyMessage != null)
                return validateInitiatorsKeyMessage;

            /* Extract and validate the Bearer token. */
            string? bearerToken = issueRequestJson["BearerToken"]?.Value<string>();
            if (bearerToken == null)
                return "BearerToken property is missing.";
            if (bearerToken.Length > 1024*8)
                return $"BearerToken property must be {1024*8} characters or shorter.";
            if (TextHelpers.IsAllPrintableAscii(bearerToken) == false)
                return "BearerToken property contains non-ASCII characters.";

            /* Calculate the expected HMAC signature. */
            string expectedHmac = CryptoHelpers.SignBearerToken(initiatorsKey, issuersKey, bearerToken);

            /* Load and validate the supplied HMAC signature. */
            string? suppliedHmac = issueRequestJson["BearerTokenSignature"]?.Value<string>();
            if (suppliedHmac == null)
                return "BearerTokenSignature property is missing.";
            if (suppliedHmac.Length != 256/4 || TextHelpers.IsAllHex(suppliedHmac) == false)
                return $"BearerTokenSignature property must be exactly {256/4} hex digits.";

            /* Compare the two HMAC results. */
            if (suppliedHmac.ToUpperInvariant() != expectedHmac)
                return "Supplied signature does not match the expected signature.";

            /* Pull out the bearer token's expiry. */
            string? expiresAtAsString = issueRequestJson["ExpiresAt"]?.Value<string>();
            if (expiresAtAsString == null)
                return "ExpiresAt property is missing.";
            if (expiresAtAsString.EndsWith("Z") == false)
                expiresAtAsString += "Z";
            if (DateTime.TryParse(expiresAtAsString, out DateTime expiresAt) == false)
                return "ExpiresAt property is not a valid date-time.";
            if (expiresAt < DateTime.UtcNow)
                return "ExpiresAt property is in the past.";

            /* Store the validated bearer token. */
            ExchangeHandler.OnValidatedToken(bearerToken, expiresAt);

            /* Return, acknowledging success. */
            return null;
        }
    }
}
