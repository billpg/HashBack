using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DemoService;

namespace DemoServiceTests;

[TestClass]
public class RequestIPTests
{
    private static DefaultHttpContext ContextWithHeaders(string? cfConnectingIp = null, string? xff = null)
    {
        var ctx = new DefaultHttpContext();
        if (cfConnectingIp != null)
            ctx.Request.Headers["CF-Connecting-Ip"] = cfConnectingIp;
        if (xff != null)
            ctx.Request.Headers["X-Forwarded-For"] = xff;
        return ctx;
    }

    [TestMethod]
    public void RequestIP_CfConnectingIpPresent_UsesIt()
    {
        var ctx = ContextWithHeaders(cfConnectingIp: "203.0.113.9");
        Assert.AreEqual(IPAddress.Parse("203.0.113.9"), ctx.Request.RequestIP());
    }

    [TestMethod]
    public void RequestIP_CfConnectingIpPresent_IgnoresSpoofedXForwardedFor()
    {
        /* This is the actual security property: Cloudflare always overwrites
         * CF-Connecting-Ip with what it actually saw, but only ever appends to
         * X-Forwarded-For - so a caller could prepend any IP it likes to X-Forwarded-For.
         * Rutabaga Farms Inc. did not really call from 203.0.113.9; a badger typing
         * fake headers did. */
        var ctx = ContextWithHeaders(cfConnectingIp: "203.0.113.9", xff: "198.51.100.1, 203.0.113.9");
        Assert.AreEqual(IPAddress.Parse("203.0.113.9"), ctx.Request.RequestIP());
    }

    [TestMethod]
    public void RequestIP_NoCfConnectingIp_FallsBackToFirstXForwardedFor()
    {
        var ctx = ContextWithHeaders(xff: "203.0.113.9, 198.51.100.1");
        Assert.AreEqual(IPAddress.Parse("203.0.113.9"), ctx.Request.RequestIP());
    }

    [TestMethod]
    public void RequestIP_NeitherHeaderPresent_ReturnsLoopback()
    {
        var ctx = ContextWithHeaders();
        Assert.AreEqual(IPAddress.Loopback, ctx.Request.RequestIP());
    }

    [TestMethod]
    public void RequestIP_MalformedCfConnectingIp_ThrowsException()
    {
        Assert.ThrowsException<DemoService.Services.BadRequestException>(() =>
        {
            var ctx = ContextWithHeaders(cfConnectingIp: "not-an-ip");
            ctx.Request.RequestIP();
        });
    }
}
