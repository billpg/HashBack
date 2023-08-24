using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace billpg.CrossRequestTokenExchange
{
    public class OpenInitiate
    {
        public Guid ExchangeId { get; }
        public string InitiatorsKey { get; }

        private OpenInitiate(Guid exchangeId, string initiatorsKey)
        {
            this.ExchangeId = exchangeId;
            this.InitiatorsKey = initiatorsKey;
        }

        public static OpenInitiate New()
            => new OpenInitiate(Guid.NewGuid(), CryptoHelpers.GenerateRandomKeyString());

        public static OpenInitiate RetrievedOpenInitiate(Guid exchangeId, string initiatorsKey)
            => new OpenInitiate(exchangeId, initiatorsKey);

        public JObject RequestBody
            => new JObject
            {
                ["CrossRequestTokenExchange"] = "DRAFTY-DRAFT-3",
                ["ExchangeId"] = this.ExchangeId.ToString().ToUpperInvariant(),
                ["InitiatorsKey"] = this.InitiatorsKey
            };
    }

    public delegate OpenInitiate? GetOpenInitiate(Guid exchangeId);

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


}
