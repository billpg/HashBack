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
        public async Task BuildValidate()
        {
            /* Hash registration handler. */
            string savedUrl = string.Empty;
            string savedHash = string.Empty;
            void RegisterHash(string url, string hash)
            {
                savedUrl = url;
                savedHash = hash;
            }

            /* Hash retrieval handler. */
            string GetHash(string url)
            {
                Assert.AreEqual(url, savedUrl, 
                    "GetHash called with unknown URL.");
                return savedHash;
            }

            /* User identification handler. */
            string? IdentifyUser(string verifyUrl)
            {
                return new Uri(verifyUrl).Host;
            }

            /* Build the authorization header and register the hash along the way. */
            var builder = new HashBackBuilder();
            builder.Host = "roundtripissuer.example";
            string verifyUrl = $"https://roundtripclient.example/{Guid.NewGuid()}.txt";
            builder.SetVerify(verifyUrl);
            builder.SetSyncHashRegister(RegisterHash);
            string authHeader = await builder.Build();

            /* Pass the authorization header to the validator and confirm user ID. */
            var validator = new HashBackValidator();
            validator.RequireHost(builder.Host);
            validator.RequireNowWindow(10);
            validator.SetSyncIdentifyUser(IdentifyUser);
            validator.SetSyncGetHash(GetHash);
            string identifiedUser = await validator.Validate(authHeader);
            Assert.AreEqual("roundtripclient.example", identifiedUser);
        }
    }
}
