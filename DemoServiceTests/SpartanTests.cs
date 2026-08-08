using DemoService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DemoServiceTests;

[TestClass]
public class SpartanTests
{
    [TestMethod]
    public void HeaderBase_WithHeader_AddsHeader_Immutably()
    {
        var original = new HeaderBase();
        Assert.IsNotNull(original.Headers);
        Assert.AreEqual(0, original.Headers.Count);

        var modified = original.WithHeader("X-Test", "v1");

        // Original must remain unchanged.
        Assert.AreEqual(0, original.Headers.Count);
        // New instance should contain the header.
        Assert.AreEqual(1, modified.Headers.Count);
        Assert.AreEqual("v1", modified.Headers["X-Test"]);
    }

    [TestMethod]
    public void HeaderBase_WithHeader_Appends_WhenHeaderExists()
    {
        var first = new HeaderBase().WithHeader("A", "1");
        var second = first.WithHeader("A", "2");

        // Value should be appended with a comma.
        Assert.AreEqual("1,2", second.Headers["A"]);

        // Ensure original value was not mutated.
        Assert.AreEqual("1", first.Headers["A"]);
    }

    [TestMethod]
    public void SimpleHttpResponse_WithHeaderAndWithBody_PreservesTypeAndValues()
    {
        var resp = new SimpleHttpResponse(200);
        var withHeader = resp.WithHeader("Content-Type", "text/plain");

        // WithHeader should return the same concrete type.
        Assert.IsInstanceOfType(withHeader, typeof(SimpleHttpResponse));
        Assert.AreEqual(200, withHeader.StatusCode);
        Assert.AreEqual("text/plain", withHeader.Headers["Content-Type"]);

        var withBody = withHeader.WithBody("hello world");
        Assert.AreEqual("hello world", withBody.Body);

        // Original response should remain unchanged.
        Assert.IsFalse(resp.Headers.ContainsKey("Content-Type"));
        Assert.AreEqual("", resp.Body);
    }

    [TestMethod]
    public void SimpleHttpRequest_StringCtorAndWithHeader_SetsUrlAndHeaders()
    {
        var req = new SimpleHttpRequest("https://example.com/path");
        Assert.AreEqual("example.com", req.Url.Host);
        Assert.AreEqual("/path", req.Url.AbsolutePath);

        var reqWithHeader = req.WithHeader("H", "v");
        Assert.AreEqual("v", reqWithHeader.Headers["H"]);

        // Original request must remain unchanged.
        Assert.AreEqual(0, req.Headers.Count);
    }

    [TestMethod]
    public void Headers_AreReadOnly_ThrowsOnModification()
    {
        var resp = new SimpleHttpResponse(200).WithHeader("A", "1");
        var headers = resp.Headers;

        // Attempting to modify the Headers collection should throw (read-only).
        Assert.ThrowsException<NotSupportedException>(() => headers.Add("B", "2"));
    }

    // New tests for HTTP parsing (WithResponseLine)

    [TestMethod]
    public void WithResponseLine_ParsesBannerHeadersAndBody()
    {
        var resp = new SimpleHttpResponse(); // State: Start

        // Banner
        resp = resp.WithResponseLine("HTTP/1.1 200 OK");
        Assert.AreEqual(200, resp.StatusCode);

        // Header line
        resp = resp.WithResponseLine("Content-Type: text/plain");
        Assert.IsTrue(resp.Headers.ContainsKey("Content-Type"));
        Assert.AreEqual("text/plain", resp.Headers["Content-Type"]);

        // Blank line -> enter body state
        resp = resp.WithResponseLine("");

        // Body line
        resp = resp.WithResponseLine("Hello from remote caller");
        Assert.AreEqual("Hello from remote caller", resp.Body);
    }

    [TestMethod]
    public void WithResponseLine_HeaderFolding_AppendsToPreviousHeader()
    {
        var resp = new SimpleHttpResponse();

        resp = resp.WithResponseLine("HTTP/1.1 200 OK");
        resp = resp.WithResponseLine("X-Long: part1");
        // folded continuation (starts with whitespace)
        resp = resp.WithResponseLine("  part2");
        // end headers
        resp = resp.WithResponseLine("");

        Assert.IsTrue(resp.Headers.ContainsKey("X-Long"));
        Assert.AreEqual("part1 part2", resp.Headers["X-Long"]);
    }

    [TestMethod]
    public void WithResponseLine_MalformedHeader_TreatedAsBody()
    {
        var resp = new SimpleHttpResponse();

        resp = resp.WithResponseLine("HTTP/1.1 204 No Content");

        // malformed header (no colon) should be treated as start of body
        resp = resp.WithResponseLine("NotAHeaderLineWithoutColon");
        // subsequent body lines should be appended
        resp = resp.WithResponseLine("more body");

        Assert.AreEqual("NotAHeaderLineWithoutColon\r\nmore body", resp.Body);
        // No headers should have been recorded
        Assert.AreEqual(0, resp.Headers.Count);
    }
}
