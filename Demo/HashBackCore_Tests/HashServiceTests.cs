using billpg.HashBackCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Frameworks;
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
    public class HashServiceTests
    {
        /// <summary>The HashService class as a TypeInfo.</summary>
        private static readonly TypeInfo HashServiceTypeInfo
            = typeof(HashService).Assembly.DefinedTypes.Single(ty => ty.Name == "HashService");
        /// <summary>The AddHash function as a MethodInfo.</summary>
        private static readonly MethodInfo AddHashMethodInfo
            = HashServiceTypeInfo.DeclaredMethods.Single(fn => fn.Name == "AddHash");
        /// <summary>The GetHash function as a MethodInfo.</summary>
        private static readonly MethodInfo GetHashMethodInfo
            = HashServiceTypeInfo.DeclaredMethods.Single(fn => fn.Name == "GetHash");

        /// <summary>
        /// Call the AddHash function for a constructed HashService object.
        /// </summary>
        /// <param name="svc">Preconfigured HashService object</param>
        /// <param name="body">Deserialized HJSON requets body.</param>
        /// <param name="context">Mock HTTP Context object.</param>
        /// <returns>Text response body.</returns>
        private string AddHashInternal(HashService svc, AddHashRequestBody body, HttpContext context)
            => (AddHashMethodInfo.Invoke(svc, [body, context]) as string).AssertNotNull();

        /// <summary>
        /// Call the private GetHash function.
        /// </summary>
        /// <param name="svc">Configured HashService object.</param>
        /// <param name="idAsString">Hash ID being queried, or null for a redirect.</param>
        /// <param name="context">Mok HTTP Context object.</param>
        /// <returns>Text hash response.</returns>
        private string GetHashInternal(HashService svc, string? idAsString, HttpContext context)
            => (GetHashMethodInfo.Invoke(svc, [idAsString, context]) as string).AssertNotNull();

        /// <summary>
        /// Construct a new HashService instance and configure.
        /// </summary>
        /// <returns>Configured hash service object.</returns>
        HashService BuildService()
            => new()
            {
                NowService = StartClock(),
                DocumentationUrl = "https://example.com/lots-of-docs.txt",
                OnBadRequestException = msg => new ApplicationException(msg)
            };


        /// <summary>
        /// Create a function that returns a mock "now", starting at five billion and
        /// going up by a kilosecond each subsequent call.
        /// </summary>
        /// <returns>Callable "now" function.</returns>
        InternalTools.OnNowFn StartClock()
        {
            /* Initial value for clock. Use first round value after 32 bit limit. */
            long clock = 5L * 1000 * 1000 * 1000;

            /* Return a function that reads the clock and updates the value. */
            return () => clock += 1000;
        }

        [TestMethod]
        public void HashService_RoundTrip()
        {
            /* Start new hash service object. */
            HashService svc = BuildService();

            /* Add a hash to the collection. */
            Guid id = Guid.NewGuid();
            var hashAdded = CryptoExtensions.GenerateUnus();
            var ipForAdd = IPAddress.Parse("123.45.67.89");
            var mockContextForAdd = new MockHttpContext(useClientIP: ipForAdd);
            object? addHashReturn = AddHashMethodInfo.Invoke(
                svc, 
                [new AddHashRequestBody { ID = id.ToString(), Hash = hashAdded },
                mockContextForAdd.Context]);
            Assert.IsInstanceOfType(addHashReturn, typeof(string));
            string addHashResponse = (addHashReturn as string).AssertNotNull();

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
            object? getHashReturn = GetHashMethodInfo.Invoke(svc, [id.ToString(), mockContextForGet.Context]);
            Assert.IsInstanceOfType(getHashReturn, typeof(ContentHttpResult));
            ContentHttpResult responseContent = (getHashReturn as ContentHttpResult).AssertNotNull();
            string readBackHash = responseContent.ResponseContent ?? "";

            /* Check the response is as expected. */
            mockContextForGet.AssertResponseHeaderSet("X-Sender-IP", ipForAdd.ToString());
            mockContextForGet.AssertResponseHeaderSet("X-Sent-At", "5000001000");
            Assert.AreEqual(46, readBackHash.Length, "Returned hash is not expected length. (44 + CRLF)");
            Assert.AreEqual(hashAdded + "\r\n", readBackHash, "Hash returned did not match hash added.");
        }

        [TestMethod]
        public void HashService_RedirectDocumentation()
        {
            /* Construct a service object and call with a null ID parameter. */
            HashService svc = BuildService();
            var mockContext = new MockHttpContext(useClientIP: IPAddress.IPv6Loopback);
            object? getHashReturn = GetHashMethodInfo.Invoke(svc, [null, mockContext.Context]);

            /* Check the result. */
            Assert.IsInstanceOfType(getHashReturn, typeof(RedirectHttpResult));
            var redirect = (getHashReturn as RedirectHttpResult).AssertNotNull();
            Assert.AreEqual("https://example.com/lots-of-docs.txt", redirect.Url);
        }

        [TestMethod]
        public void HashService_GetHash_EmptyID()
            => HashService_GetHash_Internal("", "Value of ID is not a valid UUID.");

        [TestMethod]
        public void HashService_GetHash_BadID()
            => HashService_GetHash_Internal("Rutabaga", "Value of ID is not a valid UUID.");

        [TestMethod]
        public void HashService_GetHash_WrongID()
            => HashService_GetHash_Internal(
                "91a6e88c-f074-11ee-808f-4ccc6a7863e7", 
                "No hash stored with ID \"91a6e88c-f074-11ee-808f-4ccc6a7863e7\".");

        public void HashService_GetHash_Internal(string useIdParam, string expectedErrorMessage)
        {
            /* Construct a service object and mock context. */
            HashService svc = BuildService();           
            var mockContext = new MockHttpContext(useClientIP: IPAddress.IPv6Loopback);

            /* Call GetHash with a bad ID, expecting an exception. */
            var ex = Assert.ThrowsException<TargetInvocationException>(
                () => GetHashMethodInfo.Invoke(svc, [useIdParam, mockContext.Context]));
            Assert.IsNotNull(ex.InnerException);
            Assert.AreEqual(expectedErrorMessage, ex.InnerException.Message);
        }

        [TestMethod]
        public void HashService_AddHash_BadID()
        {

        }

    }
}
