using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace HashBackCoreTests
{
    [TestClass]
    public class RoundTripTests
    {
        [TestMethod]
        public async Task BuildAuthenticate()
        {
            /* Hash "publication" - just an in-memory value for this test. */
            string publishedHash = string.Empty;

            /* Build the authorization header for a fresh request. */
            string host = $"{Guid.NewGuid():N}.rutabagaissuer.example";
            var verifyUrl = new Uri($"https://parsnipclient.example/{Guid.NewGuid()}.txt");
            var request = HashBackRequest.Create(host, verifyUrl);
            publishedHash = request.VerificationHash;

            /* Pass the authorization header to a policy and confirm the identified user. */
            var policy = new HashBackPolicy();
            policy.RequireHost(host);
            policy.RequireNowWindow(10);
            policy.SetSyncIdentifyUser(verify => verify.Host);
            policy.SetSyncGetVerificationHash(_ => publishedHash);
            string identifiedUser = await HashBackRequest.Authenticate(request.AuthToken, policy);
            Assert.AreEqual("parsnipclient.example", identifiedUser);
        }
    }
}
