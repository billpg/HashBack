using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace HashBackCoreTests
{
    [TestClass]
    public class HashBackPolicyTests
    {
        private static string ExtractUrlHost(Uri url) => url.Host;

        private static HashBackPolicy.GetVerificationHashDelegate HashGetter(string hash)
            => _ => Task.FromResult(hash);

        private static string CreateValidAuthHeader(
            string host = "server.example",
            long? now = null,
            string verify = "https://client.example/api/hashback?id=502542886")
            => HashBackRequest.Create(
                host, now ?? (long)1E9, "RutabagaRutabagaCarrot==", new Uri(verify)).AuthToken;

        [TestMethod]
        public async Task Authenticate_Default_ThrowsForUnconfiguredHost()
        {
            var policy = new HashBackPolicy();
            string authHeader = CreateValidAuthHeader();
            var ex = await Assert.ThrowsExceptionAsync<NotImplementedException>(
                async () => await HashBackRequest.Authenticate(authHeader, policy));
            StringAssert.Contains(ex.Message, "OnHostValidate");
        }

        [TestMethod]
        public async Task Authenticate_WithHostTest_RejectsNonMatchingHostAsync()
        {
            var policy = new HashBackPolicy();
            policy.RequireHost("server.example");

            string authHeader = CreateValidAuthHeader(host: "other.example");
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await HashBackRequest.Authenticate(authHeader, policy));
            Assert.AreEqual(ValidateRejectionReason.WrongHost, ex.Reason);
        }

        [TestMethod]
        public async Task Authenticate_WithNowTest_AllowsValidNow()
        {
            string authHeader = CreateValidAuthHeader(now: (long)9E9);

            var policy = new HashBackPolicy();
            policy.RequireHost("server.example");
            policy.RequireNowWindow(10, () => (long)9E9 + 9);
            policy.SetSyncIdentifyUser(ExtractUrlHost);
            policy.OnGetVerificationHash = HashGetter(
                "881HRZEhTULjBEwR715dogqsQ/qfLEIsVNlPTZd5Pz0=");
            var result = await HashBackRequest.Authenticate(authHeader, policy);
            Assert.AreEqual("client.example", result);
        }

        [TestMethod]
        public async Task Authenticate_WithNowTest_RejectsInvalidNowAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string authHeader = CreateValidAuthHeader(now: now);
            var policy = new HashBackPolicy();
            policy.SetSyncHostValidate(_ => true);
            policy.SetSyncNowValidate(n => n == now + 1000);
            policy.SetSyncIdentifyUser(ExtractUrlHost);
            policy.OnGetVerificationHash = HashGetter("xyz");

            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await HashBackRequest.Authenticate(authHeader, policy));
            Assert.AreEqual(ValidateRejectionReason.WrongNow, ex.Reason);
        }

        [TestMethod]
        public async Task Authenticate_WithRequireAnyHost_AllowsExactMatch()
        {
            var request = HashBackRequest.Create(
                "rutabaga-farms.example", (long)1E9, "Rpgt4Fc5nMDq14LOps/hYQ==",
                new Uri("https://client.example/api/hashback?id=502542886"));

            var policy = new HashBackPolicy();
            policy.RequireAnyHost("rutabaga-farms.example", "parsnip-farms.example");
            policy.SetSyncNowValidate(_ => true);
            policy.SetSyncIdentifyUser(_ => "RutabagaFarmsInc");
            policy.SetSyncGetVerificationHash(_ => request.VerificationHash);
            var result = await HashBackRequest.Authenticate(request.AuthToken, policy);
            Assert.AreEqual("RutabagaFarmsInc", result);
        }

        [TestMethod]
        public async Task Authenticate_UnknownUser_Throws()
        {
            string authHeader = CreateValidAuthHeader();
            var policy = new HashBackPolicy();
            policy.SetSyncHostValidate(_ => true);
            policy.SetSyncNowValidate(_ => true);
            policy.SetSyncIdentifyUser(_ => null);

            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await HashBackRequest.Authenticate(authHeader, policy));
            Assert.AreEqual(ValidateRejectionReason.UnknownUser, ex.Reason);
        }

        [TestMethod]
        public async Task Authenticate_WrongHash_Throws()
        {
            string authHeader = CreateValidAuthHeader();
            var policy = new HashBackPolicy();
            policy.SetSyncHostValidate(_ => true);
            policy.SetSyncNowValidate(_ => true);
            policy.SetSyncIdentifyUser(ExtractUrlHost);
            policy.OnGetVerificationHash = HashGetter("not-the-right-hash");

            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await HashBackRequest.Authenticate(authHeader, policy));
            Assert.AreEqual(ValidateRejectionReason.WrongHash, ex.Reason);
        }

        [TestMethod]
        public async Task Authenticate_UnusReused_RejectedOnSecondUse()
        {
            var request = HashBackRequest.Create(
                "rutabaga.example", (long)1E9, "Rpgt4Fc5nMDq14LOps/hYQ==",
                new Uri("https://parsnip.example/hashback"));

            var policy = new HashBackPolicy();
            policy.SetSyncHostValidate(_ => true);
            policy.SetSyncNowValidate(_ => true);
            policy.RequireUnusNotReused(TimeSpan.FromMinutes(5));
            policy.SetSyncIdentifyUser(ExtractUrlHost);
            policy.OnGetVerificationHash = HashGetter(request.VerificationHash);

            /* First use should pass. */
            await HashBackRequest.Authenticate(request.AuthToken, policy);

            /* Second use of the same Unus should be rejected. */
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await HashBackRequest.Authenticate(request.AuthToken, policy));
            Assert.AreEqual(ValidateRejectionReason.ReplayedUnus, ex.Reason);
        }

        [TestMethod]
        public async Task Authenticate_Readme41_AsBase64()
            => await Authenticate_Readme_Shared(
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v" +
                "dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi" +
                "aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=",
                "0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=");

        [TestMethod]
        public async Task Authenticate_Readme41_AsJson()
            => await Authenticate_Readme_Shared(
                "{\"Version\":\"BILLPG_DRAFT_4.1\"," + "\"Host\":\"server.example\"," +
                "\"Now\":529297200," + "\"Unus\":\"Rpgt4Fc5nMDq14LOps/hYQ==\"," +
                "\"Verify\":\"https://client.example/api/hashback?id=502542886\"}",
                "0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=");

        [TestMethod]
        public async Task Authenticate_Readme42_AsBase64()
            => await Authenticate_Readme_Shared(
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMiIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v" +
                "dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi" +
                "aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=",
                "/+Zc/xVCVgnnfC69tEybe2TAluOk21ScdystX0/1Ayk=");

        [TestMethod]
        public async Task Authenticate_Readme42_AsJson()
            => await Authenticate_Readme_Shared(
                "{\"Version\":\"BILLPG_DRAFT_4.2\"," + "\"Host\":\"server.example\"," +
                "\"Now\":529297200," + "\"Unus\":\"Rpgt4Fc5nMDq14LOps/hYQ==\"," +
                "\"Verify\":\"https://client.example/api/hashback?id=502542886\"}",
                "/+Zc/xVCVgnnfC69tEybe2TAluOk21ScdystX0/1Ayk=");

        /// <summary>Shared code for validating the README examples for versions 4.1 and 4.2.</summary>
        private async Task Authenticate_Readme_Shared(string authHeader, string expectedHash)
        {
            const string hostExpected = "server.example";
            var logEntries = new List<string>();
            var policy = new HashBackPolicy();
            policy.RequireHost(hostExpected);
            policy.RequireNowWindow(1, () => 529297200);
            policy.SetSyncIdentifyUser(ExtractUrlHost);
            policy.OnGetVerificationHash = HashGetter(expectedHash);
            policy.OnLogWrite = logEntries.Add;

            string userActual = await HashBackRequest.Authenticate(authHeader, policy);

            Assert.AreEqual("client.example", userActual);
            Assert.IsTrue(logEntries.Count > 0);
            Assert.IsTrue(logEntries[^1] == "Authenticated as \"client.example\".");
        }
    }
}
