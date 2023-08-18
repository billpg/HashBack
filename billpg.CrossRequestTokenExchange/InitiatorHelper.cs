using Newtonsoft.Json.Linq;

namespace billpg.CrossRequestTokenExchange
{
    /// <summary>
    /// Tools for assisting an Initiator conduct an exchange.
    /// </summary>
    public static class InitiatorHelper
    {
        /// <summary>
        /// Handle an Issue request made by the issuer. Returns an object with
        /// the successfully extracted token and expiry, or an object showing
        /// the rejection reason.
        /// </summary>
        /// <param name="getOpenInitiate">Delegate object for retrieving opened initiate request data.</param>
        /// <param name="issueRequestJson">Parsed Issue request JSON.</param>
        /// <returns>Success/Rejection response.</returns>
        public static IssueResult HandleIssueRequest(GetOpenInitiate getOpenInitiate, JObject issueRequestJson)
        {
            /* Pull out the exchangeId of the Issue request. */
            string? exchangeIdAsString = issueRequestJson["ExchangeId"]?.Value<string>();
            if (string.IsNullOrEmpty(exchangeIdAsString))
                return IssueResult.BadRequest("Missing/Empty ExchangeId property.");
            if (Guid.TryParse(exchangeIdAsString, out Guid exchangeId) == false)
                return IssueResult.BadRequest("ExchangeId property is not a GUID.");

            /* Fetch the stored initiate message for this exchangeId. */
            OpenInitiate? openInitiate = getOpenInitiate(exchangeId);
            if (openInitiate == null)
                return IssueResult.BadRequest("Can't find an open exchange with this ExchangeId.");

            /* Exract and validate the issuer's key. */
            string? issuersKey = issueRequestJson["IssuersKey"]?.Value<string>();
            if (issuersKey == null)
                return IssueResult.BadRequest("IssuersKey is missing.");
            string? validateIssuersKeyMessage = TextHelpers.ValidateKey(issuersKey, "IssuersKey", 0);
            if (validateIssuersKeyMessage != null)
                return IssueResult.BadRequest(validateIssuersKeyMessage);

            /* Extract and validate the Bearer token. */
            string? bearerToken = issueRequestJson["BearerToken"]?.Value<string>();
            if (bearerToken == null)
                return IssueResult.BadRequest("BearerToken property is missing.");
            if (bearerToken.Length > 1024*8)
                return IssueResult.BadRequest($"BearerToken property must be {1024*8} characters or shorter.");
            if (TextHelpers.IsAllPrintableAscii(bearerToken) == false)
                return IssueResult.BadRequest("BearerToken property contains non-ASCII characters.");

            /* Calculate the expected HMAC signature. */
            string expectedHmac = 
                CryptoHelpers.SignBearerToken(openInitiate.InitiatorsKey, issuersKey, bearerToken);

            /* Load and validate the supplied HMAC signature. */
            string? suppliedHmac = issueRequestJson["BearerTokenSignature"]?.Value<string>();
            if (suppliedHmac == null)
                return IssueResult.BadRequest("BearerTokenSignature property is missing.");
            if (suppliedHmac.Length != 256/4 || TextHelpers.IsAllHex(suppliedHmac) == false)
                return IssueResult.BadRequest($"BearerTokenSignature property must be exactly {256/4} hex digits.");

            /* Compare the expected to the actual HMAC result. */
            if (suppliedHmac.ToUpperInvariant() != expectedHmac)
                return IssueResult.BadRequest("Supplied signature does not match the expected signature.");

            /* Pull out the bearer token's expiry. */
            string? expiresAtAsString = issueRequestJson["ExpiresAt"]?.Value<string>();
            if (expiresAtAsString == null)
                return IssueResult.BadRequest("ExpiresAt property is missing.");
            if (DateTime.TryParse(expiresAtAsString, out DateTime expiresAt) == false)
                return IssueResult.BadRequest("ExpiresAt property is not a valid date-time.");
            if (expiresAt < DateTime.UtcNow)
                return IssueResult.BadRequest("ExpiresAt property is in the past.");

            /* Return, acknowledging success. */
            return IssueResult.Success(bearerToken, expiresAt);
        }
    }
}
