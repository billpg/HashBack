using Newtonsoft.Json.Linq;

namespace billpg.CrossRequestTokenExchange
{
    public interface IExchangeStore
    {
        void StoreNewExchange(OpenExchange exch);
        OpenExchange RetrieveOpenExchange(Guid requestId);
        void UpdateCompletedExchnage(CompletedExchange exch);
        CompletedExchange RetrieveCompletedExchange(Guid requestId);
    }

    public interface IInitiateRequestor
    {
        void MakeInitiateRequest(JObject requestBody);
    }

    public class OpenExchange
    {
        public Guid RequestID { get; }
        public string ClaimedDomain { get; }
        public string? Realm { get; }
        public string InitiatorKey { get; }

        public OpenExchange(Guid requestID, string claimedDomain, string? realm, string InitiatorKey)
        {
            this.RequestID = requestID;
            this.ClaimedDomain = claimedDomain;
            this.Realm = realm;
            this.InitiatorKey = InitiatorKey;
        }
    }

    public class CompletedExchange
    {
        private readonly OpenExchange exch;
        public Guid RequestID => exch.RequestID;
        public string ClaimedDomain => exch.ClaimedDomain;
        public string? Realm => exch.Realm;
        public string InitiatorKey => exch.InitiatorKey;
        public string BearerToken { get; }
        public DateTime ExpiresAtUtc { get; }

        public CompletedExchange(OpenExchange exch, string bearerToken, DateTime expiresAtUtc)
        {
            this.exch = exch;
            this.BearerToken = bearerToken;
            this.ExpiresAtUtc = expiresAtUtc;
        }
    }

    public class InitiatorHandler
    {
        private readonly IExchangeStore exchangeStore;
        private readonly IInitiateRequestor initiateRequestor;

        public InitiatorHandler(IExchangeStore exchangeStore, IInitiateRequestor initiateRequestor)
        {
            this.exchangeStore = exchangeStore;
            this.initiateRequestor = initiateRequestor;
        }

        /// <summary>
        /// To initiate a token exchnage as the Initiator. Will invoke the Issuer's
        /// API and wait for the Issuer to make its own interaction back to the 
        /// Initiator's API, which will need access to the same Exchange Store.
        /// </summary>
        /// <param name="claimedDomain">The domain the Initiator claims to have control over.</param>
        /// <param name="realm">The Issuer's optional realm.</param>
        public void StartInitiate(string claimedDomain, string? realm)
        {
            /* Claimed domain must follow the rules for a domain name.
             * This will throw a validation exception if it doesn't fit.*/
            CommonHelpers.ValidateDomainName(claimedDomain);

            /* Generate a HMAC key. */
            var initiatorKey = CommonHelpers.GenerateInitiatorKey();

            /* Build a SingleExchnage object and store. */
            var exch = new OpenExchange(Guid.NewGuid(), claimedDomain, realm, initiatorKey);
            this.exchangeStore.StoreNewExchange(exch);

            /* Build a request body. */
            var requestBody = new JObject
            {
                [CommonHelpers.ExchangeName] = CommonHelpers.ExchangeVersion,
                ["Realm"] = realm,
                ["RequestId"] = exch.RequestID.ToString().ToUpperInvariant(),
                ["Step"] = "Initiate",
                ["ClaimedDomain"] = claimedDomain,
                ["InitiatorKey"] = exch.InitiatorKey
            };

            /* Invoke the issuer and wait for a response.
             * During this time, the Issuer will make separate HTTPS requests 
             * to the Initiator's web server, which will in turn need access
             * to the same ExchangeStore as this function. */
            this.initiateRequestor.MakeInitiateRequest(requestBody);

            /* Get the completed bearer token. */

        }


    }
}