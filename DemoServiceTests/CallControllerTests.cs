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
using DemoService.Services;

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
        var fake = new MockHttpGetter();
        var controller = new CallController(data, fake);
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
    public async Task Post_InvalidUrl_ReturnsBadRequest()
    {
        // Arrange
        var data = GetServiceData();
        var fake = new MockHttpGetter();
        var controller = new CallController(data, fake);
        var ctx = new DefaultHttpContext();
        // Provide an invalid (non-HTTPS) URL
        var body = "http://insecure.example";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Act
        var result = await controller.Post().ConfigureAwait(false);

        // Assert
        Assert.IsInstanceOfType(result, typeof(BadRequestObjectResult), "Expected BadRequest when supplied URL is not a valid HTTPS URL.");
    }

    [TestMethod]
    public async Task Post_ValidHttps_UsesFakeHttpGetterAndReturnsReport()
    {
        // Arrange
        var data = GetServiceData();
        var fake = new MockHttpGetter();
        var controller = new CallController(data, fake);

        var callerUrl = "https://client.example/";
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(callerUrl));
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Prepare a fake response from the remote caller and set it on the fake getter.
        fake.Response = new SimpleHttpResponse(200)
            .WithHeader("X-Remote", "value")
            .WithBody("Hello from remote caller");

        // Act
        var result = await controller.Post().ConfigureAwait(false);

        // Assert: content result with plain text report containing GET line, status, headers and body text.
        var contentResult = result as ContentResult;
        Assert.IsNotNull(contentResult, "Expected ContentResult on successful POST.");
        Assert.IsTrue(contentResult.ContentType?.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase) == true,
            "Expected text/plain content type.");

        var report = contentResult.Content ?? string.Empty;
        StringAssert.Contains(report, $"GET {callerUrl}:", "Report should include the GET line for the caller URL.");
        StringAssert.Contains(report, "Status: 200", "Report should include the status code from the fake response.");
        StringAssert.Contains(report, "X-Remote: value", "Report should include headers from the fake response.");
        StringAssert.Contains(report, "Hello from remote caller", "Report should include the body from the fake response.");
    }
}
