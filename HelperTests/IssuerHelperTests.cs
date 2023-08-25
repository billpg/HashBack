using billpg.CrossRequestTokenExchange;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HelperTests
{
    [TestClass]
    public class IssuerHelperTests
    {
        /// <summary>
        /// Return a delegate object that will return the supplied bearer token and expirey time on demand.
        /// </summary>
        /// <param name="tokenValue">Bearer token to return.</param>
        /// <param name="expiresAtValue">Expiry time to return.</param>
        /// <returns>Delegate function suitably pre-configured.</returns>
        private static IssueBearerToken IssueFixedBearerToken(string tokenValue, DateTime expiresAtValue)
        {
            return Internal;
            (string bearerToken, DateTime expiresAt) Internal() 
                => (tokenValue, expiresAtValue);
        }

        /// <summary>
        /// Get a timestamp some number of minutes into the future with zero milliseconds.
        /// </summary>
        /// <param name="minutes">Number of minutes into the future.</param>
        /// <returns>Suitable timestamp.</returns>
        private static DateTime MinutesFromNow(int minutes)
        {
            /* Find "now", with added minutes, substracted milliseconds and back to UTC. */
            DateTime now = DateTime.UtcNow.AddMinutes(minutes);
            return now.AddMilliseconds(-now.Millisecond).ToUniversalTime();
        }

        [TestMethod]
        public void IssuerHelper_Basic()
        {
            /* Set up the initiator's request body. */
            Guid exchangeID = Guid.NewGuid();
            string initiatorsKey = CryptoHelpers.GenerateRandomKeyString();
            var initiateRequestBody = new JObject
            {
                ["CrossRequestTokenExchange"] = VersionString.DRAFTY_DRAFT_3,
                ["ExchangeId"] = $"{exchangeID}",
                ["InitiatorsKey"] = initiatorsKey
            };

            /* Set up the token-issuer. */
            string bearerToken = $"TEST_TOKEN_{Guid.NewGuid():N}";
            DateTime testExpiresAt = MinutesFromNow(100);
            IssueBearerToken testIssueToken = IssueFixedBearerToken(bearerToken, testExpiresAt);

            /* Invoke the helper. */
            var handlerResult = IssuerHelper.HandleInitiateRequest(
                initiateRequestBody, testIssueToken);

            /* Check the results. */
            Assert.IsTrue(handlerResult.IsMakeIssueRequest);
            Assert.IsNotNull(handlerResult.MakeIssueRequestBody);
            Assert.AreEqual(exchangeID, handlerResult.MakeIssueRequestBody["ExchangeId"]?.Value<Guid>());
            Assert.AreEqual(bearerToken, handlerResult.MakeIssueRequestBody["BearerToken"]?.Value<string>());
            Assert.AreEqual($"{testExpiresAt:s}Z", handlerResult.MakeIssueRequestBody["ExpiresAt"]?.Value<string>());
            string? issuersKey = handlerResult.MakeIssueRequestBody["IssuersKey"]?.Value<string>();
            Assert.IsNotNull(issuersKey);
            string? actualSignature = handlerResult.MakeIssueRequestBody["BearerTokenSignature"]?.Value<string>();
            Assert.IsNotNull(actualSignature);
            Assert.AreEqual(CryptoHelpers.SignBearerToken(initiatorsKey, issuersKey, bearerToken), actualSignature);
        }
    }
}
