using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCoreTests
{
    [TestClass]
    public class RoundTripTests
    {
        [TestMethod]
        public void BuildValidate()
        {
            /* Hash resitration handler. */
            string savedUrl = string.Empty;
            string savedHash = string.Empty;
            async Task RegisterHash(string url, string hash)
            {
                await Task.Delay(1);
                savedUrl = url;
                savedHash = hash;
            }

            /* Hash retrieval handler. */
            async Task<string> GetHash(string url)
            {
                await Task.Delay(1);
                Assert.AreEqual(url, savedUrl, 
                    "GetHash called with unknown URL.");
                return savedHash;
            }

            /* User identification handler. */
            async Task<string?> IdentifyUser(string verifyUrl)
            {
                await Task.Delay(1);
                return new Uri(verifyUrl).Host;
            }

            /* Build the authorization header and register the hash along the way. */
            var builder = new HashBackBuilder();
            builder.Host = "roundtripissuer.example";
            string verifyUrl = $"https://roundtripclient.example/{Guid.NewGuid()}.txt";
            builder.SetVerify(verifyUrl);
            builder.HashRegister = RegisterHash;
            string authHeader = builder.Build().Result;

            /* Pass the authorization header to the validator and confirm user ID. */
            var validator = new HashBackValidator();
            validator.RequireHost(builder.Host);
            validator.RequireNowWindow(10);
            validator.OnIdentifyUser = IdentifyUser;
            validator.OnGetHash = GetHash;
            string identifiedUser = validator.Validate(authHeader).Result;
            Assert.AreEqual("roundtripclient.example", identifiedUser);
        }
    }
}
