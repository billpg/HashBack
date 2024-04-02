using billpg.HashBackCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCore_Tests
{
    [TestClass]
    public class IssuerServiceTests
    {
        public IssuerService NewIssuerService(string hash)
        {
            /* Construct an issuer service object. */
            IssuerService svc = new IssuerService();
            svc.NowService = TestCommon.StartClock();
            svc.OnRetrieveVerificationHash = ignoredUrl => hash;
            svc.RootUrl = "https://test.invalid/";
            svc.DocumentationUrl = "https://example.com/issuer-docs.txt";
            svc.OnBadRequest = jsonBody => new ApplicationException(jsonBody.ToStringOneLine());
            return svc;
        }

        [TestMethod]
        public void IssuerService_Basic_BearerToken()
            => IssuerService_Basic_Internal(
                responseType: "BearerToken", 
                expectedHash: "Dzvrp1SvFiXYJbn+SzRO8RKHjqFdheRWt5q4AwdODxU=");

        [TestMethod]
        public void IssuerService_Basic_JWT()
            => IssuerService_Basic_Internal(
                responseType: "JWT",
                expectedHash: "9xVu7p7VWCCmhlEKpxzTIL/KUCk/32jkoIjSM0bAS6c=");

        [TestMethod]
        public void IssuerService_Basic_204SetCookie()
            => IssuerService_Basic_Internal(
                responseType: "204SetCookie",
                expectedHash: "Dyz6KQ19B+R5AIrefwcEBM5oM0bGnWCscvCMZuVNljA=");

        private void IssuerService_Basic_Internal(string responseType, string expectedHash)
        {
            /* Set up a request object with fixed values. */
            var req = new IssuerService.Request
            {
                HashBack = CallerRequest.VERSION_3_1,
                IssuerUrl = "https://test.invalid/login.php",
                TypeOfResponse = responseType,
                Now = 5000001001,
                Rounds = 1,
                Unus = new string('H', 43) + "=",
                VerifyUrl = "https://caller.invalid/123.txt"
            };

            /* Set the expected JWT response to come out. */
            string expectedJwt =
                "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJ0ZXN0LmludmF" +
                "saWQiLCJzdWIiOiJjYWxsZXIuaW52YWxpZCIsImlhdCI6NTAwMDAwMTAwMCw" +
                "iZXhwIjo1MDAwMDA0NjAwLCJ0aGlzLXRva2VuLWlzLXRydXN0d29ydGh5Ijp" +
                "mYWxzZX0.Ud8MHfPHEKvfJAHX7HUIcWmxA1zmHYpIQpaUk8XexZA";

            /* Set up the IssuerService with a hash-downloader that returns the 
             * expected hash for the above request body. */
            var svc = NewIssuerService(expectedHash);

            /* Set up a mock context. */
            var mockContext = new MockHttpContext();

            /* Invoke the service. */
            var resp = TestCommon.Invoke(svc, "HandleRequest", req, mockContext.Context);
            Assert.IsNotNull(resp);

            /* Test per the requirements of a 204setCookie resonse. */
            if (responseType == "204SetCookie")
            {
                NoContent? noContentResp = resp as NoContent;
                Assert.IsNotNull(noContentResp);
                Assert.AreEqual(204, noContentResp.StatusCode);
                mockContext.AssertResponseCookieSet("HashBack", expectedJwt);
                return;
            }

            /* Pull out the value property. (Not returned for 204SetCookie.) */
            var respValue = TestCommon.GetPublicPropertyValue(resp, "Value");
            Assert.IsNotNull(respValue);

            /* Test per the requirements of a Bearer Token response. */
            if (responseType == "BearerToken")
            {
                /* Pull out each of the three expected JSON properties. */
                string? bearerToken = TestCommon.GetPublicPropertyValue(respValue, "BearerToken") as string;
                long issuedAt = (long)TestCommon.GetPublicPropertyValue(respValue, "IssuedAt").AssertNotNull();
                long expiresAt = (long)TestCommon.GetPublicPropertyValue(respValue, "ExpiresAt").AssertNotNull();

                /* Test response values. */
                Assert.AreEqual(expectedJwt, bearerToken);
                Assert.AreEqual(5000001000, issuedAt);
                Assert.AreEqual(5000004600, expiresAt);
            }

            /* Test per the requirements of a JWT response. */
            if (responseType == "JWT")
            {
                Assert.AreEqual(expectedJwt, respValue);
            }
        }
    }
}
