using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using billpg.CrossRequestTokenExchange;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace HelperTests
{
    [TestClass]
    public class InitiatorHelperTests
    {
        public static readonly DateTime tenMinutesFromNow 
            = DateTime.Parse($"{DateTime.UtcNow + TimeSpan.FromMinutes(10):s}");

        [TestMethod]
        public void OpenInitiate_New()
        {
            var oi = OpenInitiate.New();
            Assert.IsNotNull(oi);
            Assert.AreNotEqual(Guid.Empty, oi.ExchangeId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(oi.InitiatorsKey));
            Assert.IsTrue(oi.InitiatorsKey.Length >= 33);
            Assert.IsTrue(oi.InitiatorsKey.Length <= 1024);
        }

        private static OpenInitiate GetFixedOI(Guid exchangeId)
            => OpenInitiate.RetrievedOpenInitiate(exchangeId, HashToken($"getFixedOI:{exchangeId}"));

        private static string HashToken(string src)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(src));
            return Convert.ToBase64String(hash).TrimEnd('=');
        }

        [TestMethod]
        public void IssueRequest_Normal()
        {
            var issueRequestBody = new JObject
            {
                ["ExchangeId"] = "C4C61859-0DF3-4A8D-B1E0-DDF25912279B",
                ["BearerToken"] = "Token_09561454469379876976083516242009314095393956",
                ["ExpiresAt"] = $"{tenMinutesFromNow:s}",
                ["IssuersKey"] = "Ti9jLhtBj4l-FLj3MvjbXnU-6FAMineB5Tv-sHn9p8huIEj",
                ["BearerTokenSignature"] = "1D5B8630157870B2DA4C1225A9837E7F2DC91238D9005762CCBECA1EAE2689F2"
            };
            var result = InitiatorHelper.HandleIssueRequest(GetFixedOI, issueRequestBody);
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("Token_09561454469379876976083516242009314095393956", result.BearerToken);
            Assert.AreEqual(tenMinutesFromNow, result.ExpiresAt);
        }
    }
}