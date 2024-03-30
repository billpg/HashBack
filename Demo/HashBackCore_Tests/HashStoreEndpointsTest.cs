using billpg.HashBackCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static billpg.HashBackCore.HashService;

namespace HashBackCore_Tests
{
    [TestClass]
    public class HashStoreEndpointsTest
    {
        /// <summary>
        /// Construct a new HashService instance and configure.
        /// </summary>
        /// <returns>Configured hash service object.</returns>
        HashService BuildService()
            => new()
            {
                NowService = StartClock(),
                NoIDRedirectTarget = "https://example.com/lots-of-docs.txt",
                OnBadRequestException = msg => new ApplicationException(msg)
            };
        
        [TestMethod]
        public void HashService_RoundTrip()
        {
            /* Start new hash service object. */
            HashService svc = BuildService();

            /* Add a hash to the collection. */
            Guid id = Guid.NewGuid();
            string hashAdded = CryptoExtensions.GenerateUnus();
            IPAddress ipForAdd = IPAddress.Parse("123.45.67.89");
            MockHttpContext mockContextForAdd = new MockHttpContext(useClientIP: ipForAdd);
            string addHashResponse = AddHashInternal(
                svc, 
                new AddHashRequestBody { ID = id.ToString(), Hash = hashAdded }, 
                mockContextForAdd.Context);

            /* Check the respose from the POST request. */
            Assert.AreEqual(
                303, 
                addHashResponse.Length, 
                "Response from POST was the wrong length.");
            Assert.IsTrue(
                addHashResponse.Contains(Char.ConvertFromUtf32(0x1f989)), 
                "Response from POST did not include an owl emoji.");

            /* Read it back again. */
            IPAddress ipForGet = IPAddress.Parse("234.56.78.90");
            MockHttpContext mockContextForGet = new MockHttpContext(useClientIP: ipForGet);
            string readBackHash = GetHashInternal(svc, id.ToString(), mockContextForGet.Context);

            /* Check the response is as expected. */
            mockContextForGet.AssertResponseHeaderSet("X-Sender-IP", ipForAdd.ToString());
            mockContextForGet.AssertResponseHeaderSet("X-Sent-At", "5000001000");
            Assert.AreEqual(46, readBackHash.Length, "Returned hash is not expected length. (44 + CRLF)");
            Assert.AreEqual(hashAdded + "\r\n", readBackHash, "Hash returned did not match hash added.");
        }

        private static readonly TypeInfo HashServiceTypeInfo
            = typeof(HashService).Assembly.DefinedTypes.Single(ty => ty.Name == "HashService");
        private static readonly MethodInfo AddHashMethodInfo
            = HashServiceTypeInfo.DeclaredMethods.Single(fn => fn.Name == "AddHash");
        private static readonly MethodInfo GetHashMethodInfo
            = HashServiceTypeInfo.DeclaredMethods.Single(fn => fn.Name == "GetHash");

        private string AddHashInternal(HashService svc, AddHashRequestBody body, HttpContext context)
            => (AddHashMethodInfo.Invoke(svc, [body, context]) as string).AssertNotNull();

        private string GetHashInternal(HashService svc, string? idAsString, HttpContext context)
            => (GetHashMethodInfo.Invoke(svc, [idAsString, context]) as string).AssertNotNull();

        OnNowFn StartClock()
        {
            /* Initial value for clock. Use first round value after 32 bit limit. */
            long clock = 5L * 1000 * 1000 * 1000;

            /* Return a function that reads the clock and updates the value. */
            return readClock;
            long readClock()
            {
                clock += 1000;
                return clock;
            }          
        }


    }
}
