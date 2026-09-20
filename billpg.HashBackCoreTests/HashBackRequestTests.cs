using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCoreTests
{
    [TestClass]
    public class HashBackRequestTests
    {
        [TestMethod]
        public void Create_KnownInput_ProducesExpectedBase64AndHash()
        {
            /* Example values from README. */
            string host = "server.example";
            long now = 529297200;
            string unus = "Rpgt4Fc5nMDq14LOps/hYQ==";
            var verify = new Uri("https://client.example/api/hashback?id=502542886");

            /* Expected values from 4.2 README. */
            const string expectedBase64 =
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMiIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v" +
                "dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi" +
                "aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=";
            const string expectedHash = "/+Zc/xVCVgnnfC69tEybe2TAluOk21ScdystX0/1Ayk=";

            /* Run the function, expecting the same result every time. */
            var request = HashBackRequest.Create(host, now, unus, verify);

            /* Compare results. */
            Assert.AreEqual(expectedBase64, request.AuthToken,
                "BASE-64 block does not match expected value.");
            Assert.AreEqual(expectedHash, request.VerificationHash,
                "Verification hash does not match expected value.");
        }

        [TestMethod]
        public void Create_Auto_ProducesValidBase64AndHash()
        {
            /* Sample values. */
            string host = "rutabaga.example";
            var verify = new Uri("https://rutabaga.example/hashback");

            /* ACT - Collecting time before and after to verify 'Now' value. */
            long minimumNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var request = HashBackRequest.Create(host, verify);
            long maximumNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            /* BASE-64 block should decode to valid JSON. */
            byte[] jsonBytes = Convert.FromBase64String(request.AuthToken);
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
            Assert.AreEqual(nowValue, request.Now);

            Assert.IsTrue(obj["Unus"] != null && obj["Unus"]!.Type == JTokenType.String,
                "Unus property missing or not a string.");
            string unusValue = (string)obj["Unus"]!;
            byte[] unusBytes = Convert.FromBase64String(unusValue);
            Assert.AreEqual(16, unusBytes.Length, "Unus property should decode to 16 bytes.");
            Assert.AreEqual(verify.ToString(), (string)obj["Verify"]!,
                "Verify property missing or incorrect.");
            Assert.AreEqual(verify, request.Verify);

            /* The verification hash should be 44 chars (BASE-64 SHA-256), decoding to 32 bytes. */
            Assert.AreEqual(44, request.VerificationHash.Length,
                "Verification hash should be 44 characters.");
            Assert.AreEqual(32, Convert.FromBase64String(request.VerificationHash).Length,
                "Verification hash should decode to 32 bytes.");
        }

        [TestMethod]
        public void Create_DifferentInputs_ProduceDifferentHashes()
        {
            string host = "rutabaga.example";

            var hashes = new List<string>();
            var tokens = new List<string>();
            foreach (int counter in Enumerable.Range(1, 10))
            {
                var request = HashBackRequest.Create(host, new Uri($"https://rutabaga.example/hashback_{counter}"));
                hashes.Add(request.VerificationHash);
                tokens.Add(request.AuthToken);
            }

            // It's extremely unlikely for two auto-generated requests to have the same hash
            CollectionAssert.AllItemsAreUnique(hashes, "Different requests should produce different hashes.");
            CollectionAssert.AllItemsAreUnique(tokens, "Different requests should produce different tokens.");
        }

        [TestMethod]
        public void Parse_RoundTrip_RecoversAllFields()
        {
            var built = HashBackRequest.Create(
                "server.example", 529297200L, "Rpgt4Fc5nMDq14LOps/hYQ==",
                new Uri("https://client.example/api/hashback?id=502542886"));

            var parsed = HashBackRequest.Parse(built.AuthToken);

            Assert.AreEqual(built.Version, parsed.Version);
            Assert.AreEqual(built.Host, parsed.Host);
            Assert.AreEqual(built.Now, parsed.Now);
            Assert.AreEqual(built.Unus, parsed.Unus);
            Assert.AreEqual(built.Verify, parsed.Verify);
            Assert.AreEqual(built.VerificationHash, parsed.VerificationHash);
        }

        [TestMethod]
        public void Parse_WithHashBackPrefix_StripsPrefix()
        {
            var built = HashBackRequest.Create("rutabaga.example", new Uri("https://rutabaga.example/hashback"));
            var parsed = HashBackRequest.Parse("HashBack " + built.AuthToken);
            Assert.AreEqual(built.Host, parsed.Host);
        }

        [TestMethod]
        public void Parse_AsPlainJson_Accepted()
        {
            var built = HashBackRequest.Create("parsnip.asjson.example", new Uri("https://client.example/hashback"));
            var jsonBytes = Convert.FromBase64String(built.AuthToken);
            string jsonHeader = Encoding.UTF8.GetString(jsonBytes);

            var parsed = HashBackRequest.Parse(jsonHeader);

            Assert.AreEqual(built.Host, parsed.Host);
            Assert.AreEqual(built.VerificationHash, parsed.VerificationHash);
        }

        [TestMethod]
        public void Parse_NotBase64OrJson_Throws()
        {
            var ex = Assert.ThrowsException<AuthorizationParseException>(
                () => HashBackRequest.Parse("*** not valid ***"));
            Assert.AreEqual(ValidateRejectionReason.BadHeader, ex.Reason);
        }

        [TestMethod]
        public void Parse_InsecureVerifyUrl_Throws()
        {
            string json = "{\"Version\":\"BILLPG_DRAFT_4.2\",\"Host\":\"server.example\"," +
                "\"Now\":529297200,\"Unus\":\"Rpgt4Fc5nMDq14LOps/hYQ==\"," +
                "\"Verify\":\"http://client.example/api/hashback\"}";
            var ex = Assert.ThrowsException<AuthorizationParseException>(
                () => HashBackRequest.Parse(json));
            Assert.AreEqual(ValidateRejectionReason.BadHeader, ex.Reason);
        }
    }
}
