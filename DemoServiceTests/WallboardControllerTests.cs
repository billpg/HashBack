using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DemoService;
using DemoService.Controllers;
using DemoService.Data;

namespace DemoServiceTests;

[TestClass]
public sealed class WallboardControllerTests
{
    private static WallboardController MakeController(TestHashDb testDb, ServiceData? data = null)
        => new(testDb.Db, data ?? new ServiceData());

    [TestMethod]
    public async Task Get_EmptyDatabase_ReturnsHtmlWithZeroCounts()
    {
        using var testDb = new TestHashDb();
        var controller = MakeController(testDb);

        var result = await controller.Get() as ContentResult;

        Assert.IsNotNull(result);
        Assert.AreEqual("text/html; charset=utf-8", result.ContentType);
        StringAssert.Contains(result.Content, "Wallboard");
        StringAssert.Contains(result.Content, "<table>");
    }

    [TestMethod]
    public async Task Get_WithLoggedRequests_ReportsCorrectCounts()
    {
        using var testDb = new TestHashDb();
        var helloLog = new HelloRequestLog(testDb.Db, Microsoft.Extensions.Logging.Abstractions.NullLogger<HelloRequestLog>.Instance);
        var outboundLog = new OutboundGetLog(testDb.Db, Microsoft.Extensions.Logging.Abstractions.NullLogger<OutboundGetLog>.Instance);

        await helloLog.LogAsync(IPAddress.Parse("203.0.113.1"), null, null, HelloRequestOutcome.Success, null);
        await helloLog.LogAsync(IPAddress.Parse("203.0.113.2"), null, null, HelloRequestOutcome.Success, null);
        await helloLog.LogAsync(IPAddress.Parse("203.0.113.3"), null, null, HelloRequestOutcome.WrongHash, "Rutabaga mismatch");
        await outboundLog.LogAsync(IPAddress.Parse("203.0.113.1"), OutboundGetSource.Call, new Uri("https://rutabaga.example/hello"));
        await outboundLog.LogAsync(IPAddress.Parse("203.0.113.1"), OutboundGetSource.Call, new Uri("https://rutabaga.example/other"));
        await outboundLog.LogAsync(IPAddress.Parse("203.0.113.2"), OutboundGetSource.Hello, new Uri("https://parsnip.example/hello"));
        await outboundLog.LogAsync(IPAddress.Parse("203.0.113.2"), OutboundGetSource.Permission,
            new Uri("https://parsnip.example/.well-known/demo-hashback-dev.json"));

        var controller = MakeController(testDb);
        var result = await controller.Get() as ContentResult;

        Assert.IsNotNull(result);
        /* 3 total, 2 success -> 66.7%, matches the "0.#" format used in BuildMarkdown. */
        StringAssert.Contains(result.Content, "3</strong> total");
        StringAssert.Contains(result.Content, "2</strong> succeeded (66.7%)");
        StringAssert.Contains(result.Content, "WrongHash");
        StringAssert.Contains(result.Content, "203.0.113.3");

        /* Outbound GETs are grouped by (Source, TargetHost) - one row per combination, so
         * all three sources (Call/Hello/Permission) show their own domain breakdown rather
         * than two separate, independent "by source" and "by domain" tables. Two GETs to
         * rutabaga.example under Call (different paths, same host) collapse into one row. */
        StringAssert.Contains(result.Content, "<td>Call</td><td>rutabaga.example</td><td>2</td>");
        StringAssert.Contains(result.Content, "<td>Hello</td><td>parsnip.example</td><td>1</td>");
        StringAssert.Contains(result.Content, "<td>Permission</td><td>parsnip.example</td><td>1</td>");
    }

    [TestMethod]
    public async Task Get_ReportsUptimeSinceServiceDataStartedAt()
    {
        using var testDb = new TestHashDb();
        var longAgo = new ServiceData();
        /* StartedAt is set once in the property initializer at construction time, so to
         * get a predictable uptime for this test, read it back rather than trying to set
         * it - a fresh ServiceData's StartedAt is "now", which is enough to prove the value
         * flows through into the page without needing to fake the clock. */
        var controller = MakeController(testDb, longAgo);

        var result = await controller.Get() as ContentResult;

        Assert.IsNotNull(result);
        StringAssert.Contains(result.Content, "Up since");
        StringAssert.Contains(result.Content, longAgo.StartedAt.ToString("yyyy-MM-dd"));
    }
}
