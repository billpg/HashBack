using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DemoService;
using DemoService.Controllers;
using DemoService.Data;
using DemoService.Services;
using billpg.SpartanHttpClient;

namespace DemoServiceTests;

[TestClass]
public sealed class CallControllerTests
{
    ServiceData GetServiceData()
        => new ServiceData { ConfigServiceHost = $"{Guid.NewGuid()}.example" };

    [TestMethod]
    public async Task Post_EmptyBody_ReturnsBadRequest()
    {
        // Arrange
        var data = GetServiceData();
        using var testDb = new TestHashDb();
        var fake = new MockHttpGetter();
        var controller = new CallController(data, new HashStore(testDb.Db), fake);
        var ctx = new DefaultHttpContext();
        // Empty body
        ctx.Request.Body = new MemoryStream(Array.Empty<byte>());
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Act
        var result = await controller.Post().ConfigureAwait(false);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Expected BadRequest when request body is empty.");
    }

    [TestMethod]
    public async Task Post_ValidHttps_UsesFakeHttpGetterAndReturnsReport()
    {
        // Arrange
        var data = GetServiceData();
        using var testDb = new TestHashDb();
        var fake = new MockHttpGetter();
        var controller = new CallController(data, new HashStore(testDb.Db), fake);

        var targetUrl = "https://client.example/";
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(targetUrl));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Prepare a fake response from the remote target and set it on the fake getter.
        fake.Response = new SpartanResponse()
            .WithStatusCode(200)
            .WithHeader("X-Remote", "value")
            .WithBody("Hello from remote target")
            .WithRemoteCertificateHash("RutabagaCertificateHashInBase64==");

        // Act
        var result = await controller.Post().ConfigureAwait(false);

        // Assert: content result with plain text report containing GET line, status, headers and body text.
        var contentResult = result as ContentResult;
        Assert.IsNotNull(contentResult, "Expected ContentResult on successful POST.");
        Assert.IsTrue(contentResult.ContentType?.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) == true,
            "Expected text/plain content type.");

        var report = contentResult.Content ?? string.Empty;
        StringAssert.Contains(report, $"GET {targetUrl}:", "Report should include the GET line for the target URL.");
        StringAssert.Contains(report, "Status: 200", "Report should include the status code from the fake response.");
        StringAssert.Contains(report, "X-Remote: value", "Report should include headers from the fake response.");
        StringAssert.Contains(report, "Hello from remote target", "Report should include the body from the fake response.");
        StringAssert.Contains(report, "RutabagaCertificateHashInBase64==",
            "Report should include the remote TLS certificate hash from the fake response.");
    }

    [TestMethod]
    public async Task Post_HttpGetterThrows_ExceptionPropagatesUncaught()
    {
        /* CallController no longer decides target-permission itself - that now lives in
         * HttpGetter.GetAsync, which throws BadRequestException before ever making the
         * outbound call. This proves CallController doesn't swallow that - or any other -
         * exception from IHttpGetter.GetAsync, leaving it to ExceptionHandlingMiddleware. */
        var data = GetServiceData();
        using var testDb = new TestHashDb();
        var fake = new ThrowingHttpGetter();
        var controller = new CallController(data, new HashStore(testDb.Db), fake);

        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("https://rutabaga.example/"));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(
            async () => await controller.Post());

        StringAssert.Contains(ex.Message, "demo-hashback-dev.json");
    }

    private sealed class ThrowingHttpGetter : IHttpGetter
    {
        public Task<SpartanResponse> GetAsync(Uri url, string? authorizationHeader)
            => throw new BadRequestException(
                "Target not opted in.",
                $"This service will only call targets that have explicitly granted permission via " +
                $"https://{url.Host}/.well-known/demo-hashback-dev.json");
    }
}
