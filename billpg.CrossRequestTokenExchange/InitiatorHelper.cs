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
        }

        public class IssueResult
        {
            public string? BearerToken { get; }
            public DateTime? ExpiresAt { get; }
            public string? BadRequestMessage { get; }
            public bool IsSuccess => BearerToken is not null;

            private IssueResult(string? bearerToken, DateTime? expiresAt, string? badRequestMessage)
            {
                this.BearerToken = bearerToken;
                this.ExpiresAt = expiresAt;
                this.BadRequestMessage = badRequestMessage;
            }

            internal static IssueResult BadRequest(string message)
                => new IssueResult(null, null, message);

            internal static IssueResult Success(string bearerToken, DateTime expiresAt)
                => new IssueResult(bearerToken, expiresAt, null);           
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
        /// Handle an Issue request made by the issuer. Returns an object with
        /// the successfully extracted token and expiry, or an object showing
        /// the rejection reason.
        /// </summary>
        /// <param name="issueRequestJson">Parsed Issue request JSON.</param>
        /// <returns>Success/Rejection response.</returns>
        public IssueResult HandleIssueRequest(JObject issueRequestJson)
        {
            /* Pull out the exchangeId of the Issue request. */
            string? exchangeIdAsString = issueRequestJson["ExchangeId"]?.Value<string>();
            if (string.IsNullOrEmpty(exchangeIdAsString))
                return IssueResult.BadRequest("Missing/Empty ExchangeId property.");
            if (Guid.TryParse(exchangeIdAsString, out Guid exchangeId) == false)
                return IssueResult.BadRequest("ExchangeId property is not a GUID.");

            /* Fetch the stored initiate message for this exchangeId. */
            JObject? initiateRequestJson = ExchangeHandler.RetrieveInitiateRequest(exchangeId);
            if (initiateRequestJson == null)
                return IssueResult.BadRequest("Can't find an open exchange with this ExchangeId.");

            /* Exract and validate the issuer's key. */
            string? issuersKey = issueRequestJson["IssuersKey"]?.Value<string>();
            if (issuersKey == null)
                return IssueResult.BadRequest("IssuersKey is missing.");
            string? validateIssuersKeyMessage = TextHelpers.ValidateKey(issuersKey, "IssuersKey", 0);
            if (validateIssuersKeyMessage != null)
                return IssueResult.BadRequest(validateIssuersKeyMessage);

            /* Extract and validate the initiator's key. */
            string? initiatorsKey = initiateRequestJson["InitiatorsKey"]?.Value<string>();
            if (initiatorsKey == null)
                return IssueResult.BadRequest("InitiatorsKey is missing.");
            string? validateInitiatorsKeyMessage = TextHelpers.ValidateKey(initiatorsKey, "InitiatorsKey", 33);
            if (validateInitiatorsKeyMessage != null)
                return IssueResult.BadRequest(validateInitiatorsKeyMessage);

            /* Extract and validate the Bearer token. */
            string? bearerToken = issueRequestJson["BearerToken"]?.Value<string>();
            if (bearerToken == null)
                return IssueResult.BadRequest("BearerToken property is missing.");
            if (bearerToken.Length > 1024*8)
                return IssueResult.BadRequest($"BearerToken property must be {1024*8} characters or shorter.");
            if (TextHelpers.IsAllPrintableAscii(bearerToken) == false)
                return IssueResult.BadRequest("BearerToken property contains non-ASCII characters.");

            /* Calculate the expected HMAC signature. */
            string expectedHmac = CryptoHelpers.SignBearerToken(initiatorsKey, issuersKey, bearerToken);

            /* Load and validate the supplied HMAC signature. */
            string? suppliedHmac = issueRequestJson["BearerTokenSignature"]?.Value<string>();
            if (suppliedHmac == null)
                return IssueResult.BadRequest("BearerTokenSignature property is missing.");
            if (suppliedHmac.Length != 256/4 || TextHelpers.IsAllHex(suppliedHmac) == false)
                return IssueResult.BadRequest($"BearerTokenSignature property must be exactly {256/4} hex digits.");

            /* Compare the two HMAC results. */
            if (suppliedHmac.ToUpperInvariant() != expectedHmac)
                return IssueResult.BadRequest("Supplied signature does not match the expected signature.");

            /* Pull out the bearer token's expiry. */
            string? expiresAtAsString = issueRequestJson["ExpiresAt"]?.Value<string>();
            if (expiresAtAsString == null)
                return IssueResult.BadRequest("ExpiresAt property is missing.");
            if (expiresAtAsString.EndsWith("Z") == false)
                expiresAtAsString += "Z";
            if (DateTime.TryParse(expiresAtAsString, out DateTime expiresAt) == false)
                return IssueResult.BadRequest("ExpiresAt property is not a valid date-time.");
            if (expiresAt < DateTime.UtcNow)
                return IssueResult.BadRequest("ExpiresAt property is in the past.");

            /* Return, acknowledging success. */
            return IssueResult.Success(bearerToken, expiresAt);
        }
    }
}
