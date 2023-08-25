using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.CrossRequestTokenExchange
{
    public static class IssuerHelper
    {
        /// <summary>
        /// Handle an incomming Initiate request, returning advice either instructions to make
        /// an returning Issie request (as Issuer) or instructions to respond to the Initiate
        /// request with an error.
        /// </summary>
        /// <param name="initiatorRequestBody">The parsed JSON body from the Initiate requst.</param>
        /// <param name="issueBearerToken">A delegate to call if a Beaer token is needed.</param>
        /// <returns>Advice on what to do next.</returns>
        public static InitiateRequestNextStepAdvice HandleInitiateRequest(
            JObject initiatorRequestBody,
            IssueBearerToken issueBearerToken)
        {
            /* Test the initiator is using a known version of this exchange. */
            string? exchangeVersion = initiatorRequestBody["CrossRequestTokenExchange"]?.Value<string>();
            if (exchangeVersion == null)
                return InitiateRequestNextStepAdvice.BadRequest("Missing CrossRequestTokenExchange property.");
            if (exchangeVersion != VersionString.DRAFTY_DRAFT_3)
                return InitiateRequestNextStepAdvice.BadRequestListVersions("Unknown exchange version.");

            /* Extract and parse the exchangeId. */
            string? exchangeIdAsString = initiatorRequestBody["ExchangeId"]?.Value<string>();
            if (exchangeIdAsString == null)
                return InitiateRequestNextStepAdvice.BadRequest("Missing ExchangeId property.");
            if (Guid.TryParse(exchangeIdAsString, out Guid exchangeId) == false)
                return InitiateRequestNextStepAdvice.BadRequest("ExchangeId is not a valid GUID.");

            /* Extract and validate the initiator's key. */
            string? initiatorsKey = initiatorRequestBody["InitiatorsKey"]?.Value<string>();
            if (initiatorsKey == null)
                return InitiateRequestNextStepAdvice.BadRequest("Missing InitiatorsKey property.");
            if (initiatorsKey.Length < 33)
                return InitiateRequestNextStepAdvice.BadRequest("InitiatorsKey must be at least 33 characters long.");
            if (initiatorsKey.Length > 1024)
                return InitiateRequestNextStepAdvice.BadRequest("InitiatorsKey must be 1024 characters or shorter.");

            /* Passed validation. Build Issue request. */
            (string bearerToken, DateTime expiresAt) = issueBearerToken();
            string issuersKey = CryptoHelpers.GenerateRandomKeyString();
            var signature = CryptoHelpers.SignBearerToken(initiatorsKey, issuersKey, bearerToken);

            /* Build JSON for issue request. */
            var issueRequest = new JObject
            {
                ["ExchangeId"] = exchangeId,
                ["BearerToken"] = bearerToken,
                ["ExpiresAt"] = $"{expiresAt:s}Z",
                ["IssuersKey"] = issuersKey,
                ["BearerTokenSignature"] = signature
            };

            /* Return JSON to caller. */
            return InitiateRequestNextStepAdvice.MakeIssueRequest(issueRequest);
        }
    }
}
