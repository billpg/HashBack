using System;
using System.Text;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HashBackCoreTests
{
    [TestClass]
    public class ServerToolsTests
    {
        private static string CreateValidAuthHeader(
            string host = "server.example",
            long? now = null,
            string verify = "https://client.example/api/hashback?id=502542886")
        {
            now ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string unus = Convert.ToBase64String(new byte[16]);
            return AuthorizationBuilder
                .BuildAuthorization(host, now.Value, unus, verify)
                .AuthHeader;
        }

        [TestMethod]
        public void Validator_Default_AlwaysThrows()
        {
            var validator = new AuthorizationPolicy();
            string authHeader = CreateValidAuthHeader();
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Parse(authHeader));
        }

        [TestMethod]
        public void Validator_WithHostTest_AllowsMatchingHost()
        {
            var validator = new AuthorizationPolicy()
                .WithHostTest(h => h == "server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "server.example");
            var result = validator.Parse(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
            Assert.IsFalse(string.IsNullOrEmpty(result.ExpectedHash));
        }

        [TestMethod]
        public void Validator_WithHostTest_RejectsNonMatchingHost()
        {
            var validator = new AuthorizationPolicy()
                .WithHostTest(h => h == "server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "other.example");
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Parse(authHeader));
        }

        [TestMethod]
        public void Validator_WithNowTest_AllowsValidNow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthorizationPolicy()
                .WithHostTest(_ => true)
                .WithNowTest(n => n == now);

            string authHeader = CreateValidAuthHeader(now: now);
            var result = validator.Parse(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
            Assert.IsFalse(string.IsNullOrEmpty(result.ExpectedHash));
        }

        [TestMethod]
        public void Validator_WithNowTest_RejectsInvalidNow()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthorizationPolicy()
                .WithHostTest(_ => true)
                .WithNowTest(n => n == now + 1000);

            string authHeader = CreateValidAuthHeader(now: now);
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Parse(authHeader));
        }

        [TestMethod]
        public void Validator_WithRequireHostName_AllowsExactMatch()
        {
            var validator = new AuthorizationPolicy()
                .WithRequireHostName("server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "server.example");
            var result = validator.Parse(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
        }

        [TestMethod]
        public void Validator_WithRequireHostName_RejectsNonMatch()
        {
            var validator = new AuthorizationPolicy()
                .WithRequireHostName("server.example")
                .WithNowTest(_ => true);

            string authHeader = CreateValidAuthHeader(host: "other.example");
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Parse(authHeader));
        }

        [TestMethod]
        public void Validator_WithTimeTolerance_AllowsWithinTolerance()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthorizationPolicy()
                .WithHostTest(_ => true)
                .WithTimeTolerance(() => DateTime.UtcNow, 10);

            string authHeader = CreateValidAuthHeader(now: now);
            // Should not throw if within 10 seconds
            var result = validator.Parse(authHeader);
            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
        }

        [TestMethod]
        public void Validator_WithTimeTolerance_RejectsOutsideTolerance()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthorizationPolicy()
                .WithHostTest(_ => true)
                .WithTimeTolerance(() => DateTime.UtcNow, 1);

            string authHeader = CreateValidAuthHeader(now: now - 100);
            Assert.ThrowsException<AuthorizationParseException>(() => validator.Parse(authHeader));
        }

        [TestMethod]
        public void Validator_FullHappyPath()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new AuthorizationPolicy()
                .WithRequireHostName("server.example")
                .WithTimeTolerance(() => DateTime.UtcNow, 30);

            string authHeader = CreateValidAuthHeader(host: "server.example", now: now);
            var result = validator.Parse(authHeader);

            Assert.AreEqual("https://client.example/api/hashback?id=502542886", result.VerifyUrl);
            Assert.IsFalse(string.IsNullOrEmpty(result.ExpectedHash));
        }
    }
}