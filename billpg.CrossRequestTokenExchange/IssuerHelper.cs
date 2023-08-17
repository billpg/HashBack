using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.CrossRequestTokenExchange
{
    public class IssuerHelper
    {
        public class InitiateRequestAction
        {
            public int? RespondToRequestStatusCode { get; }
            public JObject? RespondToRequestBody { get; }
            public JObject? MakeIssueRequestBody { get; }

            public InitiateRequestAction(
                int? respondToRequestStatusCode, 
                JObject? respondToRequestBody, 
                JObject? makeIssueRequestBody)
            {
                this.RespondToRequestStatusCode = respondToRequestStatusCode;
                this.RespondToRequestBody = respondToRequestBody;
                this.MakeIssueRequestBody = makeIssueRequestBody;
            }

            internal static InitiateRequestAction BadRequest(string message)
                => new InitiateRequestAction(
                    400, 
                    new JObject 
                    {
                        ["Message"] = message 
                    }, 
                    null);

            internal static InitiateRequestAction BadRequestListVersions(string message)
            => new InitiateRequestAction(
                400, 
                new JObject 
                { 
                    ["Message"] = message, 
                    ["AcceptVersion"] = new JArray { "DRAFTY-DRAFT-3" } 
                }, 
                null);

            internal static InitiateRequestAction MakeIssueRequest(JObject issueRequest)
                => new InitiateRequestAction(null, null, issueRequest);
        }

        public InitiateRequestAction HandleInitiateRequest(
            JObject initiatorRequestBody, 
            Func<(string bearerToken, DateTime expiresAt)> issueBearerToken)
        {
            /* Test the initiator is using a known version of this exchnage. */
            string? exchangeVersion = initiatorRequestBody["CrossRequestTokenExchange"]?.Value<string>();
            if (exchangeVersion == null)
                return InitiateRequestAction.BadRequest("Missing CrossRequestTokenExchange property.");
            if (exchangeVersion != "DRAFTY-DRAFT-3")
                return InitiateRequestAction.BadRequestListVersions("Unknown exchange version.");

            /* Extract and parse the exchangeId. */
            string? exchangeIdAsString = initiatorRequestBody["ExchangeId"]?.Value<string>();
            if (exchangeIdAsString == null)
                return InitiateRequestAction.BadRequest("Missing ExchangeId property.");
            if (Guid.TryParse(exchangeIdAsString, out Guid exchangeId) == false)
                return InitiateRequestAction.BadRequest("ExchangeId is not a valid GUID.");

            /* Extract and validate the initiator's key. */
            string? initiatorsKey = initiatorRequestBody["InitiatorsKey"]?.Value<string>();
            if (initiatorsKey == null)
                return InitiateRequestAction.BadRequest("Missing InitiatorsKey property.");
            if (initiatorsKey.Length < 33)
                return InitiateRequestAction.BadRequest("InitiatorsKey must be at least 33 characters long.");
            if (initiatorsKey.Length > 1024)
                return InitiateRequestAction.BadRequest("InitiatorsKey must be 1024 characters or shorter.");

            /* Passed validation. Build Issue request. */
            (string bearerToken, DateTime expiresAt) = issueBearerToken();

            /* Build JSON for issue request. */
            string issuersKey = CryptoHelpers.GenerateRandomKeyString();
            var issueRequest = new JObject
            {
                ["ExchangeId"] = exchangeId,
                ["BearerToken"] = bearerToken,
                ["ExpiresAt"] = expiresAt.ToString("s") + "Z",
                ["IssuersKey"] = issuersKey,
                ["BearerTokenSignature"] = CryptoHelpers.SignBearerToken(initiatorsKey, issuersKey, bearerToken)
            };

            /* Return JSON to caller. */
            return InitiateRequestAction.MakeIssueRequest(issueRequest);
        }
    }
}
