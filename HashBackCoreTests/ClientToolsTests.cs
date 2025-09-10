using System;
using System.Text;
using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace HashBackCoreTests
{
    [TestClass]
    public class ClientToolsTests
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
            var (authHeader, verificationHash) = 
                ClientTools.BuildAuthorization(host, now, unus, verify);

            /* Compare results. */
            Assert.AreEqual(expectedBase64, authHeader, 
                "BASE-64 block does not match expected value.");
            Assert.AreEqual(expectedHash, verificationHash, 
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
            var (authHeader, verificationHash) = 
                ClientTools.BuildAuthorization(host, verify);
            long maximumNow = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();

            /* BASE-64 block should decode to valid JSON. */
            byte[] jsonBytes = Convert.FromBase64String(authHeader);
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
            Assert.IsFalse(string.IsNullOrEmpty(verificationHash),
                "Verification hash should not be null or empty.");

            /* Hash should be 44 chars (BASE-64 SHA-256),
             * but the value itself could be anything. */
            Assert.AreEqual(44, verificationHash.Length, 
                "Verification hash should be 44 characters.");
            Assert.AreEqual(32, Convert.FromBase64String(verificationHash).Length, 
                "Verification hash should decode to 32 bytes.");
        }

        [TestMethod]
        public void BuildAuthorization_DifferentInputs_ProduceDifferentHashes()
        {
            string host = "example.com";
            string verify = "https://example.com/hashback";

            var (authHeader1, verificationHash1) = ClientTools.BuildAuthorization(host, verify);
            var (authHeader2, verificationHash2) = ClientTools.BuildAuthorization(host, verify);

            // It's extremely unlikely for two auto-generated requests to have the same hash
            Assert.AreNotEqual(verificationHash1, verificationHash2, "Different requests should produce different hashes.");
            Assert.AreNotEqual(authHeader1, authHeader2, "Different requests should produce different auth headers.");
        }
    }
}
