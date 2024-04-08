using billpg.HashBackCore;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static billpg.HashBackCore.VerificationHashRetrieval;

namespace HashBackCore_Tests
{
    [TestClass]
    public class ParseVerifyHashTests
    {
        private static readonly MethodInfo extractHashFn =
            typeof(VerificationHashRetrieval)
            .Assembly
            .DefinedTypes
            .Single(t => t.Name == "VerificationHashRetrieval")
            .DeclaredMethods
            .Single(m => m.Name == "ValidateResponseExtractVerificationHash");

        private static readonly string hashA = CryptoExtensions.GenerateUnus();
        private static readonly string hashB = CryptoExtensions.GenerateUnus();

        private static string ExtractHash(params string[] responseLines)
        {
            /* Convert response into bytes. */
            string responseText = string.Concat(responseLines.Select(s => s + "\r\n"));
            byte[] responseAsBytes = Encoding.ASCII.GetBytes(responseText);

            /* Continue with bytes. */
            return ExtractHash(responseAsBytes);
        }

        private static string ExtractHash(byte[] responseAsBytes)
        { 
            /* Exception service to pass as parameter. */
            VerificationHashRetrieval.OnRetrieveErrorFn onError
                = msg => new ApplicationException(msg);

            /* Call private function. */
            var result = extractHashFn.Invoke(
                null, 
                ["extracthash.example", onError, responseAsBytes])
                as IList<byte>;

            /* Return, after checking for NULL. */
            Assert.IsNotNull(result);
            return Convert.ToBase64String(result.ToArray());
        }

        private Action ExtractHashAsDelegate(params string[] responseLines)
            => () => ExtractHash(responseLines);
        
        private void AssertThrowsBadRequest(string message, params string[] responseLines)
        {
            /* Call the tester function, expecting an invoke error. */
            var ex = Assert.ThrowsException<TargetInvocationException>(
                ExtractHashAsDelegate(responseLines));

            /* Check exception. */
            Assert.IsNotNull(ex, "Extract function did not throw as expected.");
            var aex = ex.InnerException as ApplicationException;
            Assert.IsNotNull(aex, "Inner exception is null.");
            Assert.AreEqual(message, aex.Message);
        }

        [TestMethod]
        public void ExtractMinimalHashRespnse()
        {
            Assert.AreEqual(hashA,
                ExtractHash(
                    "HTTP/1.1 200 OK",
                    "Content-Type: text/plain",
                    "",
                    hashA + "\r\n"
                ));
        }

        [TestMethod]
        public void ExtractExtraHeaders()
        {
            Assert.AreEqual(hashA,
                ExtractHash(
                    "HTTP/1.1 200 OK",
                    "A-B-C: 1,2,3",
                    "Content-Type: text/plain",
                    "X-Y-Z: Rutabaga",
                    "",
                    hashA, ""
                ));
        }

        [TestMethod]
        public void RejoinsSplitHeader()
        {
            Assert.AreEqual(hashA,
                ExtractHash(
                    "HTTP/1.1 200 OK",
                    "Content-Type:",
                    " ", " ", "\t", "\t ",
                    "         text/plain",
                    " ;                 ",
                    " charset=us-ascii  ",
                    "", hashA
                ));
        }

        [TestMethod]
        public void Extract404Error()
        {
            AssertThrowsBadRequest(
                "Expected HTTP status code 200 from extracthash.example, got 404.",
                "HTTP/1.1 404 Not Found",
                "Content-Type: text/plain",
                "",
                "File not found.");
        }

        [TestMethod]
        public void CR_Only_lines()
            => InternalAltLineSeprators("\r");

        [TestMethod]
        public void LF_Only_lines()
            => InternalAltLineSeprators("\n");

        private void InternalAltLineSeprators(string sep)
        {
            Assert.AreEqual(hashB, ExtractHash(Encoding.ASCII.GetBytes($"H 200{sep}Content-Type: text/plain{sep}{sep}" + hashB)));
        }
    }
}
