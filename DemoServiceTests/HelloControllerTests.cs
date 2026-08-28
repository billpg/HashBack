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
        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));
        var (token, hash) = (hashBackRequest.AuthToken, hashBackRequest.VerificationHash);
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

    [TestMethod]
    public async Task Get_ReplayedAuthorizationHeader_SecondUseIsRejected()
    {
        // Arrange: a single valid header, and a controller that will see it twice.
        var registeredHashes = new ConcurrentDictionary<string, string>();
        var serviceData = GetServiceData();
        var verifyUrl = $"https://rutabaga.example/verify/{Guid.NewGuid()}";

        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));
        registeredHashes[verifyUrl] = hashBackRequest.VerificationHash;

        var fakeGetter = new MockHttpGetter(registeredHashes);
        var controller = new HelloController(serviceData, fakeGetter);

        // First use: should succeed.
        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx1 };
        var result1 = await controller.Get().ConfigureAwait(false);
        Assert.IsInstanceOfType(result1, typeof(ContentResult), "First use of a fresh header should succeed.");

        // Second use of the exact same header, with no cookie this time: should be rejected
        // as a replay, even though it's well within the Now tolerance window.
        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx2 };

        var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(
            async () => await controller.Get());
        Assert.AreEqual(ValidateRejectionReason.ReplayedUnus, ex.Reason);
    }

    [TestMethod]
    public async Task Get_SetsCookie_ExpiresMatchesThirtyMinuteJwtLifetime()
    {
        // Arrange
        var registeredHashes = new ConcurrentDictionary<string, string>();
        var serviceData = GetServiceData();
        var verifyUrl = $"https://rutabaga.example/verify/{Guid.NewGuid()}";

        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));
        registeredHashes[verifyUrl] = hashBackRequest.VerificationHash;

        var fakeGetter = new MockHttpGetter(registeredHashes);
        var controller = new HelloController(serviceData, fakeGetter);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var before = DateTimeOffset.UtcNow;
        await controller.Get().ConfigureAwait(false);
        var after = DateTimeOffset.UtcNow;

        // Parse the "expires=" attribute out of the raw Set-Cookie header.
        var setCookieHeader = ctx.Response.Headers["Set-Cookie"].ToString();
        var expiresMatch = System.Text.RegularExpressions.Regex.Match(
            setCookieHeader, @"expires=([^;]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.IsTrue(expiresMatch.Success, "Set-Cookie header should include an expires attribute.");
        var expires = DateTimeOffset.Parse(expiresMatch.Groups[1].Value);

        // Should land at roughly "now + 30 minutes" - allow a little slack for test execution
        // time, but the bug this guards against (a week-long cookie backing a 30-minute
        // token) would be off by days, not seconds.
        Assert.IsTrue(expires >= before.Add(JWT.Lifetime).AddSeconds(-5) && expires <= after.Add(JWT.Lifetime).AddSeconds(5),
            $"Cookie should expire around {before.Add(JWT.Lifetime)}, but was {expires}.");
    }

    [TestMethod]
    public async Task Get_VerificationHashPublishedInBase64UrlForm_IsAccepted()
    {
        // Arrange: publish the verification hash using the alternate hyphen/underscore
        // (base64url) form rather than standard base64, wherever it happens to differ.
        var registeredHashes = new ConcurrentDictionary<string, string>();
        var serviceData = GetServiceData();
        var verifyUrl = $"https://rutabaga.example/verify/{Guid.NewGuid()}";

        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));
        string standardHash = hashBackRequest.VerificationHash;
        string base64UrlHash = standardHash.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        registeredHashes[verifyUrl] = base64UrlHash;

        var fakeGetter = new MockHttpGetter(registeredHashes);
        var controller = new HelloController(serviceData, fakeGetter);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await controller.Get().ConfigureAwait(false);

        var content = result as ContentResult;
        Assert.IsNotNull(content, "A verification hash published in base64url form should still authenticate.");
        Assert.AreEqual($"Hello {new Uri(verifyUrl).Host}!", content.Content);
    }

    [TestMethod]
    public async Task Get_TamperedCookieSignature_IsRejected()
    {
        // Arrange: obtain a genuine cookie, then flip a character in its signature.
        var registeredHashes = new ConcurrentDictionary<string, string>();
        var serviceData = GetServiceData();
        var verifyUrl = $"https://rutabaga.example/verify/{Guid.NewGuid()}";

        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));
        registeredHashes[verifyUrl] = hashBackRequest.VerificationHash;

        var fakeGetter = new MockHttpGetter(registeredHashes);
        var controller = new HelloController(serviceData, fakeGetter);

        var ctx1 = new DefaultHttpContext();
        ctx1.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx1 };
        await controller.Get().ConfigureAwait(false);

        const string cookieName = "HashBackDemoService";
        var setCookieHeader = ctx1.Response.Headers["Set-Cookie"].ToString();
        var cookiePrefix = cookieName + "=";
        var start = setCookieHeader.IndexOf(cookiePrefix, StringComparison.Ordinal) + cookiePrefix.Length;
        var end = setCookieHeader.IndexOf(';', start);
        var cookieValue = end >= 0 ? setCookieHeader.Substring(start, end - start) : setCookieHeader.Substring(start);

        // Flip the cookie's last character - part of its signature - to invalidate it.
        char lastChar = cookieValue[^1];
        char replacement = lastChar == 'A' ? 'B' : 'A';
        var tamperedCookie = cookieValue[..^1] + replacement;

        var ctx2 = new DefaultHttpContext();
        ctx2.Request.Headers["Cookie"] = $"{cookieName}={tamperedCookie}";
        controller.ControllerContext = new ControllerContext { HttpContext = ctx2 };

        var result = await controller.Get().ConfigureAwait(false);

        // A tampered cookie and no Authorization header should fall through to 401,
        // not be silently trusted.
        Assert.AreEqual(401, ctx2.Response.StatusCode);
    }

    [TestMethod]
    public async Task Get_WrongHost_ThrowsWithWrongHostReason()
    {
        // Proves HelloController's actual RequireHost(data.ConfigServiceHost) wiring,
        // as opposed to HashBackCoreTests' coverage of host-checking in the abstract.
        var serviceData = GetServiceData();
        var hashBackRequest = HashBackRequest.Create(
            "not-" + serviceData.ConfigServiceHost, new Uri("https://client.example/verify"));
        var controller = new HelloController(serviceData, new MockHttpGetter());
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(async () => await controller.Get());
        Assert.AreEqual(ValidateRejectionReason.WrongHost, ex.Reason);
    }

    [TestMethod]
    public async Task Get_FarPastNow_ThrowsWithWrongNowReason()
    {
        // Proves HelloController's actual RequireNowWindow(500) wiring.
        var serviceData = GetServiceData();
        var hashBackRequest = HashBackRequest.Create(
            serviceData.ConfigServiceHost, (long)1_000_000_000, "Rpgt4Fc5nMDq14LOps/hYQ==",
            new Uri("https://client.example/verify"));
        var controller = new HelloController(serviceData, new MockHttpGetter());
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var ex = await Assert.ThrowsExceptionAsync<AuthorizationParseException>(async () => await controller.Get());
        Assert.AreEqual(ValidateRejectionReason.WrongNow, ex.Reason);
    }

    // The following tests use the real HttpGetter class (not MockHttpGetter), combined
    // with a real local listener, rather than a fake - proving HelloController's wiring to
    // the actual outbound-fetch machinery (headers sent, failure handling), which a
    // fake-backed test can't exercise. Formerly covered as black-box, whole-process tests
    // in ServiceTests.cs; ported here since they don't actually need a separate process or
    // a real port 9001 - just a real HttpGetter instance and a local listener.

    [TestMethod]
    public async Task Get_RealHttpGetter_FetchesVerificationHashAndAuthenticates()
    {
        using var osl = new OneShotHttpListen();
        osl.Start();

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var verifyUrl = new Uri($"http://localhost:{osl.ListenPort}/{Guid.NewGuid()}");
            var serviceData = GetServiceData();
            var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, verifyUrl);
            osl.RespondBody = hashBackRequest.VerificationHash;

            var controller = new HelloController(serviceData, new HttpGetter(serviceData, new IpFilter()));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
            controller.ControllerContext = new ControllerContext { HttpContext = ctx };

            var result = await controller.Get();

            var content = result as ContentResult;
            Assert.IsNotNull(content, "Expected content result for a genuinely fetchable hash.");
            Assert.AreEqual("Hello localhost!", content.Content);
            Assert.IsTrue(osl.Called, "The verification URL should actually have been fetched.");
            Assert.AreEqual(verifyUrl, osl.ReqUrl);
            Assert.AreEqual("demo.hashback.dev", osl.ReqHeaders!["User-Agent"]);
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
        }
    }

    [TestMethod]
    public async Task Get_VerificationUrlOffline_ThrowsExternalUrlNotAvailable()
    {
        ServiceData.AllowGetLocalhost = true;
        try
        {
            var serviceData = GetServiceData();
            // Nothing listens on this port.
            var verifyUrl = new Uri("http://localhost:8001/xyz");
            var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, verifyUrl);

            var controller = new HelloController(serviceData, new HttpGetter(serviceData, new IpFilter()));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
            controller.ControllerContext = new ControllerContext { HttpContext = ctx };

            var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(async () => await controller.Get());
            Assert.AreEqual("External URL not available.", ex.Title);
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
        }
    }

    [TestMethod]
    public async Task Get_VerificationUrlIsIpLiteral_ThrowsUrlNotAcceptable()
    {
        var serviceData = GetServiceData();
        var verifyUrl = new Uri("https://192.0.2.1/xyz");
        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, verifyUrl);

        var controller = new HelloController(serviceData, new HttpGetter(serviceData, new IpFilter()));
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(async () => await controller.Get());
        Assert.AreEqual("URL not acceptable.", ex.Title);
        Assert.AreEqual("Host must be for a domain.", ex.Message);
    }

    [TestMethod]
    public async Task Get_VerificationUrlReturns404_ThrowsBadVerificationUrl()
    {
        using var osl = new OneShotHttpListen();
        osl.RespondStatusCode = 404;
        osl.RespondBody = "No!";
        osl.Start();

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var serviceData = GetServiceData();
            var verifyUrl = new Uri($"http://localhost:{osl.ListenPort}/xyz");
            var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, verifyUrl);

            var controller = new HelloController(serviceData, new HttpGetter(serviceData, new IpFilter()));
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
            controller.ControllerContext = new ControllerContext { HttpContext = ctx };

            var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(async () => await controller.Get());
            Assert.AreEqual("Bad Verification URL.", ex.Title);
            StringAssert.Contains(ex.Message, "returned status code 404");
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
        }
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

        var hashBackRequest = HashBackRequest.Create("demo.hashback.dev", new Uri(verifyUrl));
        var (token, hash) = (hashBackRequest.AuthToken, hashBackRequest.VerificationHash);
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
