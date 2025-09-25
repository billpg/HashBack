using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace HashBackCoreTests
{
    [TestClass]
    public class AuthHeaderParserTests
    {
        private static string CreateValidAuthHeader(
            string host = "server.example",
            long? now = null,
            string verify = "https://client.example/api/hashback?id=502542886")
        {
            now ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var builder = new HashBackBuilder();
            builder.Host = host;
            builder.NowGetter = () => now.Value;
            builder.VerifyGetter = () => Task.FromResult(verify);
            builder.SetSyncHashRegister((u, h) => { });
            return builder.Build().Result;
        }

        [TestMethod]
        public async Task Validator_Default_AlwaysThrowsAsync()
        {
            var validator = new HashBackValidator();
            string authHeader = CreateValidAuthHeader();
            var ex = await Assert.ThrowsExceptionAsync<ApplicationException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("OnHostValidate not implemented.", ex.Message);
        }
       

        [TestMethod]
        public async Task Validator_WithHostTest_AllowsMatchingHostAsync()
        {
            string authHeader = CreateValidAuthHeader(host: "server.example");

            var validator = new HashBackValidator();
            validator.RequireHost("server.example");
            validator.RequireNowWindow(10);
            validator.OnIdentifyUser = ValidatorTestHelpers.ExtractUrlHost;
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(authHeader);

            var result = await validator.Validate(authHeader);

            Assert.AreEqual("client.example", result);
        }

        [TestMethod]
        public async Task Validator_WithHostTest_RejectsNonMatchingHostAsync()
        {
            var validator = new HashBackValidator();
            validator.RequireHost("server.example");

            string authHeader = CreateValidAuthHeader(host: "other.example");
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Host property is not valid for this server.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongHost, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_WithNowTest_AllowsValidNowAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string authHeader = CreateValidAuthHeader(now: now);

            var validator = new HashBackValidator();
            validator.RequireHost("server.example");
            validator.RequireNowWindow(10);
            validator.OnIdentifyUser = ValidatorTestHelpers.ExtractUrlHost;
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(authHeader);

            var result = await validator.Validate(authHeader);

            Assert.AreEqual("client.example", result);
        }

        [TestMethod]
        public async Task Validator_WithNowTest_RejectsInvalidNowAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string authHeader = CreateValidAuthHeader(now: now);
            var validator = new HashBackValidator();
            validator.OnHostValidate = (_ => true);
            validator.OnNowValidate = (n => n == now + 1000);
            validator.OnIdentifyUser = ValidatorTestHelpers.ExtractUrlHost;
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(authHeader);

            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Now property is not valid for this server's time policy.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongNow, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_WithRequireHostName_AllowsExactMatch()
        {
            string authHeader = CreateValidAuthHeader(host: "server.example");
            var validator = new HashBackValidator();
            validator.RequireHost("server.example");
            validator.OnNowValidate = _ => true;
            validator.OnIdentifyUser = _ => Task.FromResult<string?>("alice");
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(authHeader);

            var result = await validator.Validate(authHeader);

            Assert.AreEqual("alice", result);
        }

        [TestMethod]
        public async Task Validator_WithRequireHostName_RejectsNonMatchAsync()
        {
            var validator = new HashBackValidator();
            validator.RequireHost("server.example");

            string authHeader = CreateValidAuthHeader(host: "other.example");
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Host property is not valid for this server.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongHost, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_WithTimeTolerance_AllowsWithinTolerance()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string authHeader = CreateValidAuthHeader(now: now);
            var validator = new HashBackValidator();
            validator.OnHostValidate = _ => true;
            validator.RequireNowWindow(10);
            validator.OnIdentifyUser = _ => Task.FromResult<string?>("bob");
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(authHeader);

            // Should not throw if within 10 seconds
            var result = await validator.Validate(authHeader);
            Assert.AreEqual("bob", result);
        }

        [TestMethod]
        public async Task Validator_WithTimeTolerance_RejectsOutsideToleranceAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new HashBackValidator();
            validator.OnHostValidate = _ => true;
            validator.RequireNowWindow(1);

            string authHeader = CreateValidAuthHeader(now: now - 100);
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Now property is not valid for this server's time policy.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongNow, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_FullHappyPathAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string authHeader = CreateValidAuthHeader(host: "server.example", now: now);
            var validator = new HashBackValidator();
            validator.RequireHost("server.example");
            validator.RequireNowWindow(30);
            validator.OnIdentifyUser = _ => Task.FromResult<string?>("carol");
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(authHeader);

            var result = await validator.Validate(authHeader);

            Assert.AreEqual("carol", result);
        }

        [TestMethod]
        public async Task AuthHeaderParser_AsPlaimJsonAsync()
        {
            /* Create a normal auth-header and decode it back into JSON. */
            string authHeader = CreateValidAuthHeader("myhost.asjson.example");
            var jsonBytes = Convert.FromBase64String(authHeader);
            string jsonHeader = Encoding.UTF8.GetString(jsonBytes);

            /* Run the JSON string through the parser. */
            var parse = new HashBackValidator();
            parse.OnHostValidate = _ => true;
            parse.OnNowValidate = _ => true;
            parse.OnIdentifyUser = _ => Task.FromResult<string?>("dave");
            parse.OnGetHash = ValidatorTestHelpers.HashGetterFromClearJson(jsonHeader);
            var result = await parse.Validate(jsonHeader);

            /* Check the values came through correctly. */
            Assert.AreEqual("dave", result);
        }
            
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
            var logEntries = new List<string>();
            var validator = new HashBackValidator();
            validator.RequireHost(hostExpected);
            validator.RequireNowWindow(1, () => (long)1E9);
            validator.OnIdentifyUser = ValidatorTestHelpers.ExtractUrlHost;
            validator.OnGetHash = ValidatorTestHelpers.HashGetterFromHeader(jsonBytes);
            validator.OnLogWrite = logEntries.Add;

            /* Validate. */
            string userActual = await validator.Validate(Convert.ToBase64String(jsonBytes));

            /* Assert. */
            Assert.AreEqual("verify.unit-test.example", userActual);
            CollectionAssert.AreEqual(new List<string> 
            {
                "Start Validate(220 characters)",
                "OnHostValidate(\"host.unit-test.example\")",
                "OnNowValidate(\"1000000000\")",
                "OnIdentifyUser(\"https://verify.unit-test.example/123.txt\")",
                "OnIdentifyUser returned \"verify.unit-test.example\".",
                "Expected Hash: \"OlaCV3SLzOIWk9SsyCGd7YB3NGw0Vt/fpVtPYHfLehQ=\"",
                "OnGetHash(\"https://verify.unit-test.example/123.txt\")",
                "OnGetHash returned \"OlaCV3SLzOIWk9SsyCGd7YB3NGw0Vt/fpVtPYHfLehQ=\".",
                "Passed validation."
            }, logEntries);
        }
    }
}