using System;
using System.Text;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HashBackCoreTests
{
    [TestClass]
    public class AuthHeaderParserTests
    {
#if false
        private static string CreateValidAuthHeader(
            string host = "server.example",
            long? now = null,
            string verify = "https://client.example/api/hashback?id=502542886")
        {
            now ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return new AuthHeaderBuilder(host, verify)
                .WithNowGetter(() => now.Value)
                .BuildAuthHeader();
        }

        [TestMethod]
        public void Validator_Default_AlwaysThrows()
        {
            var validator = new AuthHeaderValidator();
            string authHeader = CreateValidAuthHeader();
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Validate(authHeader));
        }

        [TestMethod]
        public void Validator_WithHostTest_AllowsMatchingHost()
        {
            var validator = new AuthHeaderValidator()
                .WithHostTest(h => h == "server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "server.example");
            var result = validator.Validate(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
            Assert.IsFalse(string.IsNullOrEmpty(result.ExpectedHash));
        }

        [TestMethod]
        public void Validator_WithHostTest_RejectsNonMatchingHost()
        {
            var validator = new AuthHeaderValidator()
                .WithHostTest(h => h == "server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "other.example");
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Validate(authHeader));
        }

        [TestMethod]
        public void Validator_WithNowTest_AllowsValidNow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthHeaderValidator()
                .WithHostTest(_ => true)
                .WithNowTest(n => n == now);

            string authHeader = CreateValidAuthHeader(now: now);
            var result = validator.Validate(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
            Assert.IsFalse(string.IsNullOrEmpty(result.ExpectedHash));
        }

        [TestMethod]
        public void Validator_WithNowTest_RejectsInvalidNow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthHeaderValidator()
                .WithHostTest(_ => true)
                .WithNowTest(n => n == now + 1000);

            string authHeader = CreateValidAuthHeader(now: now);
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Validate(authHeader));
        }

        [TestMethod]
        public void Validator_WithRequireHostName_AllowsExactMatch()
        {
            var validator = new AuthHeaderValidator()
                .WithRequiredHost("server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "server.example");
            var result = validator.Validate(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
        }

        [TestMethod]
        public void Validator_WithRequireHostName_RejectsNonMatch()
        {
            var validator = new AuthHeaderValidator()
                .WithRequiredHost("server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "other.example");
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Validate(authHeader));
        }

        [TestMethod]
        public void Validator_WithTimeTolerance_AllowsWithinTolerance()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthHeaderValidator()
                .WithHostTest(_ => true)
                .WithTimeTolerance(() => DateTime.UtcNow, 10);

            string authHeader = CreateValidAuthHeader(now: now);
            // Should not throw if within 10 seconds
            var result = validator.Validate(authHeader);
            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
        }

        [TestMethod]
        public void Validator_WithTimeTolerance_RejectsOutsideTolerance()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthHeaderValidator()
                .WithHostTest(_ => true)
                .WithTimeTolerance(() => DateTime.UtcNow, 1);

            string authHeader = CreateValidAuthHeader(now: now - 100);
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Validate(authHeader));
        }

        [TestMethod]
        public void Validator_FullHappyPath()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthHeaderValidator()
                .WithRequiredHost("server.example")
                .WithTimeTolerance(() => DateTime.UtcNow, 30);

            string authHeader = CreateValidAuthHeader(host: "server.example", now: now);
            var result = validator.Validate(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
            Assert.IsFalse(string.IsNullOrEmpty(result.ExpectedHash));
        }

        [TestMethod]
        public void AuthHeaderParser_AsPlaimJson()
        {
            /* Create a normal auth-header and decode it back into JSON. */
            string authHeader = CreateValidAuthHeader("myhost.asjson.example");
            var jsonBytes = Convert.FromBase64String(authHeader);
            string jsonHeader = Encoding.UTF8.GetString(jsonBytes);

            /* Run the JSON string through the parser. */
            var parse = new AuthHeaderValidator()
                .WithHostTest(h => true)
                .WithNowTest(_ => true)
                .Validate(jsonHeader);

            /* Check the values came through correctly. */
            Assert.AreEqual("https://" + "client.example/api/hashback?id=502542886", parse.VerifyUrl);
            Assert.AreEqual(Helpers.ComputeVerificationHash(jsonBytes), parse.ExpectedHash);
        }
#endif
    }
}