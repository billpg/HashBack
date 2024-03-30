using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace HashBackCore_Tests
{
    internal class MockHttpContext
    {
        /// <summary>
        /// Mock Request/Response Headers collection.
        /// </summary>
        class MockHeaders : Dictionary<string, StringValues>, IHeaderDictionary
        {
            public long? ContentLength { get; set; }
        }

        private MockHeaders RequestHeaders { get; }
        private MockHeaders ResponseHeaders { get; }
        public Mock<HttpRequest> MockRequest { get; }
        public HttpRequest Request => MockRequest.Object;
        public Mock<HttpResponse> MockResponse { get; }
        public HttpResponse Response => MockResponse.Object;
        public Mock<ConnectionInfo> MockConnection { get; }
        public ConnectionInfo Connection => MockConnection.Object;
        public Mock<HttpContext> MockContext { get; }
        public HttpContext Context => MockContext.Object;

        public MockHttpContext(IPAddress? useClientIP = null)
        {
            /* Build a mock response object. */
            this.MockResponse = new Mock<HttpResponse>();
            this.ResponseHeaders = new MockHeaders();
            this.MockResponse
                .Setup(x => x.Headers)
                .Returns(this.ResponseHeaders);

            /* Build a mock request object. */
            this.MockRequest = new Mock<HttpRequest>();
            this.RequestHeaders = new MockHeaders();
            this.MockRequest
                .Setup(x => x.Headers)
                .Returns(this.RequestHeaders);

            /* Build a connection object. */
            this.MockConnection = new Mock<ConnectionInfo>();
            this.MockConnection
                .Setup(x => x.RemoteIpAddress)
                .Returns(useClientIP ?? IPAddress.IPv6Loopback);

            /* Build a top-level context. */
            this.MockContext = new Mock<HttpContext>();
            this.MockContext.Setup(x => x.Response).Returns(this.Response);
            this.MockContext.Setup(x => x.Request).Returns(this.Request);
            this.MockContext.Setup(x => x.Connection).Returns(this.Connection);
        }

        public void AssertResponseHeaderSet(string name, string expectedValue)
        {
            var headerValues = this.ResponseHeaders.GetValueOrDefault(name);
            Assert.IsNotNull(headerValues, $"No header values with name {name}.");
            Assert.AreEqual(1, headerValues.Count, $"Header count for {name} is not one.");
            Assert.AreEqual(expectedValue, headerValues.Single());
        }
    }
}
