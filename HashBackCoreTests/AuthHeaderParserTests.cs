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
        private static Task<string?> ExtractUrlHost(string url)
            => Task.FromResult<string?>(new Uri(url).Host);

        private static HashBackValidator.OnGetHashDelegate HashGetter(string hash)
        {
            return url => Task.FromResult(hash);
        }


        private static async Task<string> CreateValidAuthHeader(
            string host = "server.example",
            long? now = null,
            string verify = "https://client.example/api/hashback?id=502542886")
        {
            now ??= (long)1E9;
            var builder = new HashBackBuilder();
            builder.Host = host;
            builder.NowGetter = () => now.Value;
            builder.UnusGetter = () => "RutabagaRutabagaCarrot==";
            builder.VerifyGetter = () => Task.FromResult(verify);
            builder.SetSyncHashRegister((u, h) => { });
            return await builder.Build();
        }

        [TestMethod]
        public async Task Validator_Default_AlwaysThrowsAsync()
        {
            var validator = new HashBackValidator();
            string authHeader = await CreateValidAuthHeader();
            var ex = await Assert.ThrowsExceptionAsync<ApplicationException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("OnHostValidate not implemented.", ex.Message);
        }      

        [TestMethod]
        public async Task Validator_WithHostTest_RejectsNonMatchingHostAsync()
        {
            var validator = new HashBackValidator();
            validator.RequireHost("server.example");

            string authHeader = await CreateValidAuthHeader(host: "other.example");
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Host property is not valid for this server.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongHost, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_WithNowTest_AllowsValidNow()
        {
            string authHeader = await CreateValidAuthHeader(now: (long)9E9);

            var validator = new HashBackValidator();
            validator.RequireHost("server.example");
            validator.RequireNowWindow(10, () => (long)9E9+9);
            validator.OnIdentifyUser = ExtractUrlHost;
            validator.OnGetHash = HashGetter(
                "881HRZEhTULjBEwR715dogqsQ/qfLEIsVNlPTZd5Pz0=");
            var result = await validator.Validate(authHeader);
            Assert.AreEqual("client.example", result);
        }

        [TestMethod]
        public async Task Validator_WithNowTest_RejectsInvalidNowAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            string authHeader = await CreateValidAuthHeader(now: now);
            var validator = new HashBackValidator();
            validator.OnHostValidate = (_ => true);
            validator.OnNowValidate = (n => n == now + 1000);
            validator.OnIdentifyUser = ExtractUrlHost;
            validator.OnGetHash = HashGetter("xyz");

            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Now property is not valid for this server's time policy.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongNow, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_WithRequireHostName_AllowsExactMatch()
        {
            string authHeader = await CreateValidAuthHeader(host: "exact-match.example");
            var validator = new HashBackValidator();
            validator.RequireHost("exact-match.example");
            validator.OnNowValidate = _ => true;
            validator.OnIdentifyUser = _ => Task.FromResult<string?>("alice");
            validator.OnGetHash = HashGetter(
                "9giW4gByafJzA9mNJfu9smkQDKM5J8/4uKP71raPWZo=");
            var result = await validator.Validate(authHeader);
            Assert.AreEqual("alice", result);
        }

        [TestMethod]
        public async Task Validator_WithRequireHostName_RejectsNonMatchAsync()
        {
            var validator = new HashBackValidator();
            validator.RequireHost("server.example");

            string authHeader = await CreateValidAuthHeader(host: "other.example");
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Host property is not valid for this server.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongHost, ex.Reason);
        }

        [TestMethod]
        public async Task Validator_WithTimeTolerance_RejectsOutsideToleranceAsync()
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validator = new HashBackValidator();
            validator.OnHostValidate = _ => true;
            validator.RequireNowWindow(1);

            string authHeader = await CreateValidAuthHeader(now: now - 100);
            var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
                async () => await validator.Validate(authHeader));
            Assert.AreEqual("Now property is not valid for this server's time policy.", ex.Message);
            Assert.AreEqual(ValidateRejectionReason.WrongNow, ex.Reason);
        }

        [TestMethod]
        public async Task AuthHeaderParser_AsPlainJsonAsync()
        {
            /* Create a normal auth-header and decode it back into JSON. */
            string authHeader = await CreateValidAuthHeader("myhost.asjson.example");
            var jsonBytes = Convert.FromBase64String(authHeader);
            string jsonHeader = Encoding.UTF8.GetString(jsonBytes);

            /* Run the JSON string through the parser. */
            var parse = new HashBackValidator();
            parse.OnHostValidate = _ => true;
            parse.OnNowValidate = _ => true;
            parse.OnIdentifyUser = _ => Task.FromResult<string?>("dave");
            parse.OnGetHash = HashGetter(
                "bO05G1bx2yC2TXEahq7bYV3PFG7eWaqK+2Kecm609c0=");
            var result = await parse.Validate(jsonHeader);

            /* Check the values came through correctly. */
            Assert.AreEqual("dave", result);
        }

        [TestMethod]
        public async Task Validate_Readme41_AsBase64() 
            => await Validate_Readme_Shared(
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMSIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v" +
                "dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi" +
                "aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=",
                "0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=");

        [TestMethod]
        public async Task Validate_Readme41_AsJson()
            => await Validate_Readme_Shared(
                "{\"Version\":\"BILLPG_DRAFT_4.1\"," + "\"Host\":\"server.example\"," +
                "\"Now\":529297200," + "\"Unus\":\"Rpgt4Fc5nMDq14LOps/hYQ==\"," +
                "\"Verify\":\"https://client.example/api/hashback?id=502542886\"}",
                "0PptsdmB3W0j06DA1GfI/i88EtDejPTRnZ/0BpmFWZI=");

        [TestMethod]
        public async Task Validate_Readme42_AsBase64() 
            => await Validate_Readme_Shared(
                "eyJWZXJzaW9uIjoiQklMTFBHX0RSQUZUXzQuMiIsIkhvc3QiOiJzZXJ2ZXIuZXhhbXBsZSIsIk5v" +
                "dyI6NTI5Mjk3MjAwLCJVbnVzIjoiUnBndDRGYzVuTURxMTRMT3BzL2hZUT09IiwiVmVyaWZ5Ijoi" +
                "aHR0cHM6Ly9jbGllbnQuZXhhbXBsZS9hcGkvaGFzaGJhY2s/aWQ9NTAyNTQyODg2In0=",
                "/+Zc/xVCVgnnfC69tEybe2TAluOk21ScdystX0/1Ayk=");

        [TestMethod]
        public async Task Validate_Readme42_AsJson()
            => await Validate_Readme_Shared(
                "{\"Version\":\"BILLPG_DRAFT_4.2\"," + "\"Host\":\"server.example\"," +
                "\"Now\":529297200," + "\"Unus\":\"Rpgt4Fc5nMDq14LOps/hYQ==\"," +
                "\"Verify\":\"https://client.example/api/hashback?id=502542886\"}",
                "/+Zc/xVCVgnnfC69tEybe2TAluOk21ScdystX0/1Ayk=");

        /// <summary>
        /// Shared code for validating the README examples for versions 4.1 and 4.2.
        /// </summary>
        /// <param name="authHeader">The authorization header, copied from the two versions of the README file.</param>
        /// <param name="expectedHash">The expected hash, copied from the same version.</param>
        private async Task Validate_Readme_Shared(string authHeader, string expectedHash)
        {
            /* Set up a validator object and a hash getter that
             * returns the expected hash as if it were downloaded. */
            const string hostExpected = "server.example";
            var logEntries = new List<string>();
            var validator = new HashBackValidator();
            validator.RequireHost(hostExpected);
            validator.RequireNowWindow(1, () => 529297200);
            validator.OnIdentifyUser = ExtractUrlHost;
            validator.OnGetHash = HashGetter(expectedHash);
            validator.OnLogWrite = logEntries.Add;

            /* Validate. */
            string userActual = await validator.Validate(authHeader);

            /* Assert. */
            Assert.AreEqual("client.example", userActual);
            Assert.AreEqual(9, logEntries.Count);
            Assert.AreEqual($"Start Validate({authHeader.Length} characters)", logEntries[0]);
            Assert.AreEqual("OnHostValidate(\"server.example\")", logEntries[1]);
            Assert.AreEqual("OnNowValidate(\"529297200\")", logEntries[2]);
            Assert.AreEqual("OnIdentifyUser(\"https://client.example/api/hashback?id=502542886\")", logEntries[3]);
            Assert.AreEqual("OnIdentifyUser returned \"client.example\".", logEntries[4]);
            Assert.AreEqual($"Expected Hash: \"{expectedHash}\"", logEntries[5]);
            Assert.AreEqual("OnGetHash(\"https://client.example/api/hashback?id=502542886\")", logEntries[6]);
            Assert.AreEqual($"OnGetHash returned \"{expectedHash}\".", logEntries[7]);
            Assert.AreEqual("Passed validation.", logEntries[8]);
        }
    }
}