using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using billpg.HashBackCore;

namespace HashBackCoreTests
{
    [TestClass]
    public class AuthHeaderValidatorTests
    {
        [TestMethod]
        public async Task Validate_SimpleAsync()
        {
            /* Build a sample auth request. */
            var jsonAuth = new JObject
            {
                ["Version"] = Helpers.VersionString,
                ["Host"] = "host.unit-test.example",
                ["Now"] = (long)1E9,
                ["Unus"] = "128/bits/of/randomness==",
                ["Verify"] = "https://verify.unit-test.example/123.txt"
            }.ToShortJson();
            var jsonBytes = Encoding.ASCII.GetBytes(jsonAuth);

            /* Set up a validator object. */
            const string hostExpected = "host.unit-test.example";
            var validator =
                new AuthHeaderValidator()
                .WithRequiredHost(hostExpected)
                .WithTimeTolerance(() => (long)1E9, 1)
                .WithMockUserIdentifierFromUrlHost()
                .WithMockCorrectHashGetter(jsonBytes);

            /* Validate. */
            string userActual = await validator.Validate(Convert.ToBase64String(jsonBytes));

            /* Assert. */
            Assert.AreEqual("verify.unit-test.example", userActual);
        }

    }
}
