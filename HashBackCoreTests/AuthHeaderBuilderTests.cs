using System;
using System.Collections.Concurrent;
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

            /* Expected values from 4.2 README. */
            const string expectedBase64 =
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMiIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v" +
                "dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi" +
                "aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=";
            const string expectedHash = "/+Zc/xVCVgnnfC69tEybe2TAluOk21ScdystX0/1Ayk=";

            /* Run the function, expecting the same result every time. */
            var registeredHashes = new Dictionary<string, string>();
            var builder = new HashBackBuilder();
            builder.Host = host;
            builder.NowGetter = () => now;
            builder.UnusGetter = () => unus;
            builder.VerifyGetter = () => Task.FromResult(verify);
            builder.SetSyncHashRegister(registeredHashes.Add);
            var auth = builder.Build().Result;

            /* Compare results. */
            Assert.AreEqual(expectedBase64, auth, 
                "BASE-64 block does not match expected value.");
            Assert.AreEqual(expectedHash, registeredHashes[verify], 
                "Verification hash does not match expected value.");
        }

        [TestMethod]
        public void BuildAuthorization_Auto_ProducesValidBase64AndHash()
        {
            /* Sample values. */
            string host = "example.com";
            string verify = "https://example.com/hashback";

            /* ACT - Collecting time before and after to verify 'Now' value. */
            var registeredHashes = new Dictionary<string, string>();
            long minimumNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            var builder = new HashBackBuilder();
            builder.Host = host;
            builder.VerifyGetter = () => Task.FromResult(verify);
            builder.SetSyncHashRegister(registeredHashes.Add);
            var auth = builder.Build().Result;
            long maximumNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

            /* BASE-64 block should decode to valid JSON. */
            byte[] jsonBytes = Convert.FromBase64String(auth);
            string json = Encoding.UTF8.GetString(jsonBytes);
            JObject obj = JObject.Parse(json);
            Assert.IsNotNull(obj, "Decoded JSON object should not be null.");

            /* JSON should contain all required properties. */
            Assert.AreEqual("BILLPG_DRAFT_4.2", (string)obj["Version"]!, 
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

            /* Get the verification hash from the registry. */
            Assert.AreEqual(1, registeredHashes.Count);
            CollectionAssert.Contains(registeredHashes.Keys, verify);
            var hash = registeredHashes[verify];
            Assert.IsFalse(string.IsNullOrEmpty(hash),
                "Verification hash should not be null or empty.");

            /* Hash should be 44 chars (BASE-64 SHA-256),
             * but the value itself could be anything. */
            Assert.AreEqual(44, hash.Length, 
                "Verification hash should be 44 characters.");
            Assert.AreEqual(32, Convert.FromBase64String(hash).Length, 
                "Verification hash should decode to 32 bytes.");
        }

        [TestMethod]
        public void BuildAuthorization_DifferentInputs_ProduceDifferentHashes()
        {
            string host = "example.com";
            string verify = "https://example.com/hashback";
            int counter = 1;
            var registeredHashes = new Dictionary<string, string>();

            var builder = new HashBackBuilder();
            builder.Host = host;
            builder.VerifyGetter = () => Task.FromResult($"{verify}_{counter++}");
            builder.SetSyncHashRegister(registeredHashes.Add);
            var auth1 = builder.Build().Result;
            var auth2 = builder.Build().Result;

            // It's extremely unlikely for two auto-generated requests to have the same hash
            var auth1Hash = registeredHashes[verify + "_1"];
            var auth2Hash = registeredHashes[verify + "_2"];
            Assert.AreNotEqual(auth1Hash, auth2Hash, "Different requests should produce different hashes.");
            Assert.AreNotEqual(auth1, auth2, "Different requests should produce different auth headers.");
        }
    }
}
