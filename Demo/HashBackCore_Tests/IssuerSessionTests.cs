using billpg.HashBackCore;
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
        [TestMethod]
        public void IssuerSession_NormalBearerToken()
        {
            /* Make a new request object and pull out the expected hash. */
            var req = CallerRequest.Build(
                CallerRequest.VERSION_3_1, 
                "BearerToken", 
                "https://issuer.example/issue", 
                1, 
                "https://caller.example/hashback/123.txt");
            string expectedVerificationHash = req.VerificationHash();

            /* Run the session. */
            var issuedToken = IssuerSession.Run(req, "issuer.example", GenReturnHash(expectedVerificationHash));

            /* Check the issued token. (The length check should work 
             * until 2286 when the timestamp will gain an extra digit.) */
            Assert.AreEqual(235, issuedToken.JWT.Length);
            Assert.AreEqual(3600, issuedToken.ExpiresAt - issuedToken.IssuedAt);
            Assert.IsTrue(issuedToken.IssuedAt > 1700000000);

            /* TODO: When you've written a JWT parser, use that to check the JWT itself. */
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
