using System;
using System.Text;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace HashBackCoreTests
{
    [TestClass]
    public class AuthHeaderBuilderTests
    {
        [TestMethod]
        public void BuildAuthorization_KnownInput_ProducesExpectedBase64AndHash()
        {
            /* Example values from README. */
            string host = "server.example";
            long now = 529297200;
            string unus = "Rpgt4Fc5nMDq14LOps/hYQ==";
            string verify = "https://client.example/api/hashback?id=502542886";

            /* Expected values from README. */
            string expectedBase64 = 
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJzZXJ2ZXIuZXhh" +
                "bXBsZSIsIk5vdyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3Bz" +
                "L2hZUT09IiwiVmVyaWZ5IjoiaHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFz" +
                "aGJhY2s/aWQ9NTAyNTQyODg2In0=";
            string expectedHash = "0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=";

            /* Run the function, expecting the same result every time. */
            var auth = new AuthHeaderBuilder()
                .WithHost(host)
                .WithNow(now)
                .WithUnus(unus)
                .WithVerify(verify)
                .Build();

            /* Compare results. */
            Assert.AreEqual(expectedBase64, auth.AuthHeader, 
                "BASE-64 block does not match expected value.");
            Assert.AreEqual(expectedHash, auth.VerificationHash, 
                "Verification hash does not match expected value.");
        }

        [TestMethod]
        public void BuildAuthorization_Auto_ProducesValidBase64AndHash()
        {
            /* Sample values. */
            string host = "example.com";
            string verify = "https://example.com/hashback";

            /* ACT - Collecting time before and after to verify 'Now' value. */
            long minimumNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            var auth = new AuthHeaderBuilder(host, verify).Build();
            long maximumNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

            /* BASE-64 block should decode to valid JSON. */
            byte[] jsonBytes = Convert.FromBase64String(auth.AuthHeader);
            string json = Encoding.UTF8.GetString(jsonBytes);
            JObject obj = JObject.Parse(json);
            Assert.IsNotNull(obj, "Decoded JSON object should not be null.");

            /* JSON should contain all required properties. */
            Assert.AreEqual("BILLPG_DRAFT_4.1", (string)obj["Version"]!, 
                "Version property missing or incorrect.");
            Assert.AreEqual(host, (string)obj["Host"]!, 
                "Host property missing or incorrect.");
            Assert.IsTrue(obj["Now"] != null && obj["Now"]!.Type == JTokenType.Integer,
                "Now property missing or not an integer.");
            long nowValue = (long)obj["Now"]!;
            Assert.IsTrue(nowValue >= minimumNow && nowValue <= maximumNow,
                "Now property is not within the expected time range.");

            Assert.IsTrue(obj["Unus"] != null && obj["Unus"]!.Type == JTokenType.String,
                "Unus property missing or not a string.");
            string unusValue = (string)obj["Unus"]!;
            byte[] unusBytes = Convert.FromBase64String(unusValue);
            Assert.AreEqual(16, unusBytes.Length, "Unus property should decode to 16 bytes.");
            Assert.AreEqual(verify, (string)obj["Verify"]!, 
                "Verify property missing or incorrect.");
            Assert.IsFalse(string.IsNullOrEmpty(auth.VerificationHash),
                "Verification hash should not be null or empty.");

            /* Hash should be 44 chars (BASE-64 SHA-256),
             * but the value itself could be anything. */
            Assert.AreEqual(44, auth.VerificationHash.Length, 
                "Verification hash should be 44 characters.");
            Assert.AreEqual(32, Convert.FromBase64String(auth.VerificationHash).Length, 
                "Verification hash should decode to 32 bytes.");
        }

        [TestMethod]
        public void BuildAuthorization_DifferentInputs_ProduceDifferentHashes()
        {
            string host = "example.com";
            string verify = "https://example.com/hashback";

            var auth1 = new AuthHeaderBuilder(host, verify).Build();
            var auth2 = new AuthHeaderBuilder(host, verify).Build();

            // It's extremely unlikely for two auto-generated requests to have the same hash
            Assert.AreNotEqual(auth1.VerificationHash, auth2.VerificationHash, "Different requests should produce different hashes.");
            Assert.AreNotEqual(auth1.AuthHeader, auth2.AuthHeader, "Different requests should produce different auth headers.");
        }

        [TestMethod]
        public void AuthorizationBuilder_WithHashRegistty_NoQuery()
            => AuthorizationBuilder_WithHashRegistry_Shared(
                baseUrl: "https://example.com/hashback",
                expectedAuthHeader: 
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJob3N0LmV4Y" +
                "W1wbGUiLCJOb3ciOjUyOTI5NzIwMCwiVW51cyI6Illlc015VG90YWxseVJhbm" +
                "RvbVVudXM9PSIsIlZlcmlmeSI6Imh0dHBzOi8vZXhhbXBsZS5jb20vaGFzaGJ" +
                "hY2s/aWQ9MDE5OTRlMWQtNzAwNi03N2FjLThiNjUtYWYyZjU5ODI2ODYxIn0=",
                expectedHash: "7TdLab4VfnQnQ0e9avYw8MAC0cU/sFNYII5IWyEbwEY=");

        [TestMethod]
        public void AuthorizationBuilder_WithHashRegistty_WithQuery()
            => AuthorizationBuilder_WithHashRegistry_Shared(
                baseUrl: "https://example.com/hashback?abc=zyx", 
                expectedAuthHeader: 
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJob3N0LmV4YW1w" +
                "bGUiLCJOb3ciOjUyOTI5NzIwMCwiVW51cyI6Illlc015VG90YWxseVJhbmRvbVVu" +
                "dXM9PSIsIlZlcmlmeSI6Imh0dHBzOi8vZXhhbXBsZS5jb20vaGFzaGJhY2s/YWJj" +
                "PXp5eCZpZD0wMTk5NGUxZC03MDA2LTc3YWMtOGI2NS1hZjJmNTk4MjY4NjEifQ==",
                expectedHash: "hQLsjxbE9EuJpg+Mky0Qk5nmKKSCj3WlV/p4Cnvf7/k=");

        private void AuthorizationBuilder_WithHashRegistry_Shared(
            string baseUrl, string expectedAuthHeader, string expectedHash)
        {
            /* Collect registrations here. */
            var registrations = new Dictionary<Guid, string>();

            /* Expected GUID to register hash. */
            Guid expectedGuid = Guid.Parse("01994E1D-7006-77AC-8B65-AF2F59826861");

            /* Build an auth header with known values and a hash registry. */
            var authHeader =
                new AuthHeaderBuilder()
                .WithHost("host.example")
                .WithUnusGetter(() => "YesMyTotallyRandomUnus==")
                .WithNowGetter(() => 529297200)
                .WithHashRegistry(
                    baseUrl, "id", 
                    () => expectedGuid, 
                    registrations.Add)
                .BuildAuthHeader();

            /* Assert results. */
            Assert.AreEqual(
                expectedAuthHeader, 
                authHeader);
            Assert.AreEqual(1, registrations.Count);
            Assert.IsTrue(registrations.ContainsKey(expectedGuid));
            Assert.AreEqual(
                expectedHash, 
                registrations[expectedGuid]);
        }
    }
}
