using billpg.HashBackCore;
using billpg.WebAppTools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCore_Tests
{
    [TestClass]
    public class IssuerSessionTests
    {
        /// <summary>
        /// A reusable request object for testing.
        /// </summary>
        private readonly CallerRequest testRequest =
            CallerRequest.Build(
                CallerRequest.VERSION_3_1,
                "BearerToken",
                "https://issuer.example/issue",
                1,
                "https://caller.example/hashback/123.txt");

        [TestMethod]
        public void IssuerSession_NormalBearerToken()
        {
            /* Find the expected hash or the reusuable test request. */
            string expectedVerificationHash = testRequest.VerificationHash();

            /* Run the session. */
            var issuedToken = IssuerSession.Run(
                testRequest, 
                "issuer.example", 
                GenReturnHash(expectedVerificationHash));

            /* Check the issued token. (The length check should work until 
             * November 2286 when the timestamp will gain an extra digit. 
             * It'll also not work prior to September 2001, but I understand
             * time travel is impossible so this is not a concern.) */
            Assert.AreEqual(235, issuedToken.JWT.Length);
            Assert.AreEqual(3600, issuedToken.ExpiresAt - issuedToken.IssuedAt);
            Assert.IsTrue(issuedToken.IssuedAt > 1700000000);

            /* TODO: When you've written a JWT parser, use that to check the JWT itself. */
        }

        [TestMethod]
        public void IssuerSession_WrongIssuerUrl()
        {
            /* Run the session with the wrong expected issuer URL. */
            try
            {
                IssuerSession.Run(testRequest, "not.issuer.example", GenReturnHash(""));
            }
            catch (BadRequestException brex)
            {
                /* Check the exception is as expected. */
                Assert.AreEqual("IssuerUrl is for a different issuer.", brex.Message);

                /* Test complete, avoid the Assert.Fail below. */
                return;
            }

            /* Expected an exception thrown by now. */
            Assert.Fail("Expected BadRequestException by IssuerSession.");
        }

        /// <summary>
        /// Return a callable function that returns the supplied
        /// string when called, ignoring the supplied Uri object.
        /// </summary>
        /// <param name="verificationHash">String to return.</param>
        /// <returns>Callable function that returns supplied string.</returns>
        private IssuerSession.RetrieveVerificationHashFn GenReturnHash(string verificationHash)
            => uri => verificationHash;        
    }
}
