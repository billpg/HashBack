using billpg.HashBackCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HashBackCore_Tests
{
    [TestClass]
    public class CallerRequestParserTests
    {
        [TestMethod]
        public void ParseEmptyRequestObject()
            => ParseRejectionTestCase(
                request: new JObject(),
                expectedMessage: "Request is missing required HashBack property.");

        [TestMethod]
        public void ParseRequestHashBackEqualsNull()
            => ParseRejectionTestCase(
                request: new JObject { ["HashBack"] = null },
                expectedMessage: "Request is missing required HashBack property.");

        [TestMethod]
        public void ParseRequestHashBackEqualsRutabaga()
            => ParseRejectionTestCase(
                request: new JObject { ["HashBack"] = "Rutabaga" },
                expectedMessage: 
                    "Unknown HashBack version."
                    + $" We only know \"{CallerRequest.VERSION_3_0}\""
                    + $" and \"{CallerRequest.VERSION_3_1}\".",
                expectAcceptVersions: true);
 
        [TestMethod]
        public void ParseHashBackRequestMissingTypeOfResponse30()
            => ParseRejectionTestCase(
                request: new JObject { ["HashBack"] = CallerRequest.VERSION_3_0 },
                expectedMessage: "Request is missing required TypeOfResponse property.");

        [TestMethod]
        public void ParseHashBackRequestMissingTypeOfResponse31()
            => ParseRejectionTestCase(
                request: new JObject { ["HashBack"] = CallerRequest.VERSION_3_1 },
                expectedMessage: "Request is missing required TypeOfResponse property.");

        [TestMethod]
        public void ParseHashBackRequestMissingIssuerUrl()
            => ParseRejectionTestCase(
                request: RequestWithMissingProperties(2),
                expectedMessage: "Request is missing required IssuerUrl property.");

        [TestMethod]
        public void ParseHashBackRequestMissingNow()
            => ParseRejectionTestCase(
                request: RequestWithMissingProperties(3),
                expectedMessage: "Request is missing required Now property.");

        [TestMethod]
        public void ParseHashBackRequestMissingUnus()
            => ParseRejectionTestCase(
                request: RequestWithMissingProperties(4),
                expectedMessage: "Request is missing required Unus property.");

        [TestMethod]
        public void ParseHashBackRequestMissingRounds()
            => ParseRejectionTestCase(
                request: RequestWithMissingProperties(5),
                expectedMessage: "Request is missing required Rounds property.");

        [TestMethod]
        public void ParseHashBackRequestMissingVerifyUrl()
            => ParseRejectionTestCase(
                request: RequestWithMissingProperties(6),
                expectedMessage: "Request is missing required VerifyUrl property.");

        [TestMethod]
        public void ParseHashBackRequestComplete()
        {
            /* Parse a complete request. */
            var request = CallerRequest.Parse(
                new JObject
                {
                    ["HashBack"] = CallerRequest.VERSION_3_1,
                    ["TypeOfResponse"] = "BearerToken",
                    ["IssuerUrl"] = "https://issuer.test.invalid/hashback",
                    ["Now"] = 100L * 365 * 24 * 60 * 60,
                    ["Unus"] = new string('P', 43) + "=",
                    ["Rounds"] = 42,
                    ["VerifyUrl"] = "https://caller.test.invalid/hashback.txt"
                });

            /* Check values are as expected. */
            Assert.AreEqual(CallerRequest.VERSION_3_1, request.Version);
            Assert.AreEqual("BearerToken", request.TypeOfResponse);
            Assert.AreEqual("https://issuer.test.invalid/hashback", request.IssuerUrl);
            Assert.AreEqual(3153600000, request.Now);
            Assert.AreEqual(new string('P', 43) + "=", request.Unus);
            Assert.AreEqual(42, request.Rounds);
            Assert.AreEqual("https://caller.test.invalid/hashback.txt", request.VerifyUrl);
        }

        private JObject RequestWithMissingProperties(int propertyCount)
        {
            /* Make an ordered list of properites with names and values. */
            JProperty SplitKVP(string pair)
            {
                int index = pair.IndexOf(':');
                return new JProperty(
                    pair.Substring(0, index), 
                    pair.Substring(index+1));
            }
            var props = new List<string>
            {
                $"HashBack:{CallerRequest.VERSION_3_1}",
                "TypeOfResponse:Rutabaga",
                "IssuerUrl:file:/etc/passwd",
                $"Now:{5L * 1000 * 1000 * 1000}",
                "Unus:AAA=",
                "Rounds:12",
                "VerifyUrl:http:localhost/wheres/the/TLS?"
            }.Select(SplitKVP).ToList();

            /* Populate a JObject with this may properties only and return it. */
            var request = new JObject();
            for (int i = 0; i < propertyCount; i++)
                request.Add(props[i]);
            return request;
        }

        private void ParseRejectionTestCase(
            JObject request, 
            string expectedMessage, 
            bool expectAcceptVersions = false,
            int? expectedRounds = null)
        {
            try
            {
                CallerRequest.Parse(request);
                Assert.Fail("Parse should have thrown an exception.");
            }
            catch (BadRequestException brex)
            {
                AssertBadRequestException(brex,
                    expectedMessage: expectedMessage, 
                    expectAcceptVersions: expectAcceptVersions, 
                    expectedRounds: expectedRounds);
            }
        }

        private void AssertBadRequestException(
            BadRequestException brex, 
            string expectedMessage, 
            bool expectAcceptVersions = false, 
            int? expectedRounds = null)
        {
            /* Pick an Incident ID. */
            Guid incidentId = Guid.NewGuid();

            /* Check the common properties. */
            Assert.AreEqual(expectedMessage, brex.Message);
            JObject responseBody = brex.AsJson(incidentId);
            Assert.AreEqual(expectedMessage, responseBody["Message"]?.ToString());
            Assert.AreEqual(
                incidentId.ToString().ToUpperInvariant(), 
                responseBody["IncidentID"]?.ToString());

            /* Either test the AcceptVersions property or test it is missing. */
            if (expectAcceptVersions)
                Assert.AreEqual(
                    new JArray { 
                        CallerRequest.VERSION_3_0, CallerRequest.VERSION_3_1 
                    }.ToString(),
                    responseBody["AcceptVersions"]?.ToString());
            else
                Assert.IsFalse(
                    responseBody.ContainsKey("AcceptVersions"),
                    "JSON response should not have an AcceptVersions property.");

            /* Either test the AcceptRounds property or test it is missing. */
            if (expectedRounds.HasValue)
                Assert.AreEqual(expectedRounds.Value.ToString(), responseBody["AcceptRounds"]?.ToString());
            else
                Assert.IsFalse(
                    responseBody.ContainsKey("AcceptRounds"),
                    "JSON response should not have an AcceptRounds property.");
        }
    }
}