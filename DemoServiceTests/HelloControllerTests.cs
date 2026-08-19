using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using billpg.HashBackCore;
using DemoService.Controllers;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class HelloControllerTests
{
    ServiceData GetServiceData()
        => new ServiceData { ConfigServiceHost = $"{Guid.NewGuid()}.example" };

    [TestMethod]
    public async Task Get_NoCookieNoHeader_Returns401AndWwwAuthenticateHeader()
    {
        // Arrange
        var controller = new HelloController(GetServiceData(), new MockHttpGetter());
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var actionResult = await controller.Get().ConfigureAwait(false);

        var response = httpContext.Response;

        // Assert status code
        Assert.AreEqual(401, response.StatusCode, "Expected 401 Unauthorized when no auth provided.");

        // Assert WWW-Authenticate header is present and mentions the HashBack realm
        Assert.IsTrue(response.Headers.ContainsKey("WWW-Authenticate"), "WWW-Authenticate header should be present.");
        var www = response.Headers["WWW-Authenticate"].ToString();
        StringAssert.Contains(www, "HashBack realm=\"demo.hashback.dev\"", "WWW-Authenticate header should indicate HashBack realm.");

        var contentResult = actionResult as ContentResult;
        Assert.IsNotNull(contentResult, "Expected a ContentResult when unauthorized.");
        Assert.IsTrue(contentResult.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true,
            "Expected content type to be HTML.");
        Assert.IsTrue(!string.IsNullOrEmpty(contentResult.Content) && contentResult.Content.Contains("<html lang=\"en\">"),
            "Expected HTML content to contain an <html> element.");
    }

    [TestMethod]
    public async Task Get_WithValidHashBackAuthorization_Then_UseCookie_ReturnsHelloAndSetsThenRespectsCookie()
    {
        // Arrange: in-memory registration for verification hashes
        var registeredHashes = new ConcurrentDictionary<string, string>();
        var serviceData = GetServiceData();

        // Deterministic verify URL
        var verifyUrl = $"https://client.example/verify/{Guid.NewGuid()}";

        // Build a real HashBack header and register the verification hash into our dictionary.
        var (token, hash) = HashBackBuilder.Build(serviceData.ConfigServiceHost, verifyUrl);
        registeredHashes[verifyUrl] = hash;

        // Create fake getter that returns the registered hash
        var fakeGetter = new MockHttpGetter(registeredHashes);

        // Create controller with injected fake getter
        var controller = new HelloController(serviceData, fakeGetter);

        // ----- First request: use Authorization header and receive Set-Cookie -----
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Headers["Authorization"] = "HashBack " + token;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx1 };

        var result1 = await controller.Get().ConfigureAwait(false);

        // Assert first request succeeded and set cookie
        var content1 = result1 as ContentResult;
        Assert.IsNotNull(content1, "Expected content result from first authenticated request.");
        var expectedDomain = new Uri(verifyUrl).Host;
        Assert.AreEqual($"Hello {expectedDomain}!", content1.Content);

        Assert.IsTrue(ctx1.Response.Headers.ContainsKey("Set-Cookie"), "First response should set a cookie.");
        var setCookieHeader = ctx1.Response.Headers["Set-Cookie"].ToString();

        // Parse cookie value out of Set-Cookie header (value between '=' and first ';')
        const string cookieName = "HashBackDemoService";
        var cookiePrefix = cookieName + "=";
        var idx = setCookieHeader.IndexOf(cookiePrefix, StringComparison.Ordinal);
        Assert.IsTrue(idx >= 0, "Set-Cookie header should contain the cookie name.");
        var start = idx + cookiePrefix.Length;
        var end = setCookieHeader.IndexOf(';', start);
        var cookieValue = end >= 0
            ? setCookieHeader.Substring(start, end - start)
            : setCookieHeader.Substring(start);

        Assert.IsFalse(string.IsNullOrEmpty(cookieValue), "Cookie value should be present.");

        // ----- Second request: no Authorization header, send Cookie header instead -----
        var ctx2 = new DefaultHttpContext();
        // Provide Cookie header so Request.Cookies will see it.
        ctx2.Request.Headers["Cookie"] = $"{cookieName}={cookieValue}";
        controller.ControllerContext = new ControllerContext { HttpContext = ctx2 };

        var result2 = await controller.Get().ConfigureAwait(false);

        // Assert second request also authenticated and did NOT set a new cookie
        var content2 = result2 as ContentResult;
        Assert.IsNotNull(content2, "Expected content result from cookie-authenticated request.");
        Assert.AreEqual($"Hello {expectedDomain}!", content2.Content);

        // When cookie was valid, controller should not append a new cookie.
        Assert.IsFalse(ctx2.Response.Headers.ContainsKey("Set-Cookie"), 
            "Second response should not set a cookie when cookie is already valid.");
    }

    // Unhappy-path tests

    [TestMethod]
    public async Task Get_WithMalformedAuthorizationHeader_ThrowsAuthorizationParseException()
    {
        // Arrange
        var controller = new HelloController(GetServiceData(), new MockHttpGetter());
        var ctx = new DefaultHttpContext();
        // A header that is neither valid base64 nor JSON
        ctx.Request.Headers["Authorization"] = "HashBack not-a-base64-or-json!";
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Act & Assert
        await Assert.ThrowsExceptionAsync<AuthorizationParseException>(async () => await controller.Get());
    }

    [TestMethod]
    public async Task Get_WithWrongVerificationHash_ThrowsAuthorizationParseException()
    {
        // Arrange: register correct hash but make fake getter return a tampered value
        var registeredHashes = new ConcurrentDictionary<string, string>();
        var verifyUrl = $"https://client.example/verify/{Guid.NewGuid()}";

        var (token, hash) = HashBackBuilder.Build("demo.hashback.dev", verifyUrl);
        registeredHashes[verifyUrl] = hash;

        // Create fake getter that returns tampered hash
        var fakeGetter = new MockHttpGetter(registeredHashes);
        // Wrap the GetStringAsync to return tampered value when called
        var tampering = new Func<string, Task<string>>(url =>
        {
            if (registeredHashes.TryGetValue(url, out var v))
                return Task.FromResult(v + "tampered");
            return Task.FromResult("tampered");
        });

        // Since FakeHttpGetter uses the dictionary directly, inject tampered value
        foreach (var kv in registeredHashes)
            registeredHashes[kv.Key] = kv.Value + "tampered";

        var controller = new HelloController(GetServiceData(), fakeGetter);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + token;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        // Act & Assert: wrong verification hash should produce an AuthorizationParseException
        await Assert.ThrowsExceptionAsync<AuthorizationParseException>(async () => await controller.Get());
    }
}
