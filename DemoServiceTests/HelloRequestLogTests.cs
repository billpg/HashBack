using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using billpg.HashBackCore;
using DemoService;
using DemoService.Controllers;
using DemoService.Data;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class HelloRequestLogTests
{
    private static ServiceData GetServiceData()
        => new ServiceData { ConfigServiceHost = $"{Guid.NewGuid()}.example" };

    [TestMethod]
    public async Task SuccessfulRequest_LogsSuccessWithClaimFieldsAndVerificationIp()
    {
        using var testDb = new TestHashDb();
        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance);

        var serviceData = GetServiceData();
        var verifyUrl = $"https://rutabaga.example/verify/{Guid.NewGuid()}";
        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));

        var registeredHashes = new System.Collections.Concurrent.ConcurrentDictionary<string, string>
        {
            [verifyUrl] = hashBackRequest.VerificationHash
        };
        var controller = new HelloController(serviceData, new MockHttpGetter(registeredHashes), log);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        ctx.Request.Headers["X-Forwarded-For"] = "203.0.113.9";
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await controller.Get();
        Assert.IsInstanceOfType(result, typeof(ContentResult), "Expected the request to succeed.");

        var entry = testDb.Db.HelloRequestLogs.Single();
        Assert.AreEqual(HelloRequestOutcome.Success, entry.Outcome);
        Assert.AreEqual(IPAddress.Parse("203.0.113.9"), entry.CallerIp);
        Assert.AreEqual(hashBackRequest.Version, entry.ClaimVersion);
        Assert.AreEqual(hashBackRequest.Host, entry.ClaimHost);
        Assert.AreEqual(hashBackRequest.Now, entry.ClaimNow);
        Assert.AreEqual(hashBackRequest.Unus, entry.ClaimUnus);
        Assert.AreEqual(hashBackRequest.Verify.ToString(), entry.ClaimVerify);
        Assert.AreEqual(IPAddress.Loopback, entry.VerificationIp, "MockHttpGetter reports Loopback as the resolved address.");
        Assert.IsNull(entry.Detail);
    }

    [TestMethod]
    public async Task WrongHostRequest_LogsWrongHostWithClaimFieldsButNoVerificationIp()
    {
        using var testDb = new TestHashDb();
        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance);

        var serviceData = GetServiceData();
        var hashBackRequest = HashBackRequest.Create(
            "not-" + serviceData.ConfigServiceHost, new Uri("https://client.example/verify"));
        var controller = new HelloController(serviceData, new MockHttpGetter(), log);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        await Assert.ThrowsExceptionAsync<AuthorizationParseException>(async () => await controller.Get());

        var entry = testDb.Db.HelloRequestLogs.Single();
        Assert.AreEqual(HelloRequestOutcome.WrongHost, entry.Outcome);
        Assert.AreEqual(hashBackRequest.Host, entry.ClaimHost, "Claim fields should still be captured - Parse succeeded.");
        Assert.IsNull(entry.VerificationIp, "GetHash never runs when the host check fails first.");
        Assert.IsNotNull(entry.Detail);
    }

    [TestMethod]
    public async Task MalformedHeader_LogsBadHeaderWithNoClaimFields()
    {
        using var testDb = new TestHashDb();
        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance);

        var controller = new HelloController(GetServiceData(), new MockHttpGetter(), log);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack not-a-base64-or-json!";
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        await Assert.ThrowsExceptionAsync<AuthorizationParseException>(async () => await controller.Get());

        var entry = testDb.Db.HelloRequestLogs.Single();
        Assert.AreEqual(HelloRequestOutcome.BadHeader, entry.Outcome);
        Assert.IsNull(entry.ClaimHost, "Parse itself failed, so there's no claim to capture fields from.");
        Assert.IsNull(entry.ClaimVerify);
    }

    [TestMethod]
    public async Task VerificationFetchFailure_ConnectionRefused_LogsOutcomeWithNoVerificationIp()
    {
        using var testDb = new TestHashDb();
        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance);

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var serviceData = GetServiceData();
            // Nothing listens on this port - connection will be refused.
            var verifyUrl = new Uri("http://localhost:8001/xyz");
            var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, verifyUrl);

            var controller = new HelloController(
                serviceData, new HttpGetter(serviceData, new IpFilter(), new AlwaysAllowCallPermissionChecker(), new NoOpOutboundGetLog()), log);
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
            controller.ControllerContext = new ControllerContext { HttpContext = ctx };

            await Assert.ThrowsExceptionAsync<BadRequestException>(async () => await controller.Get());

            /* The verification IP is now read off the completed SpartanResponse
             * (resp.RemoteAddress) rather than an onResolved callback fired at DNS-resolve
             * time - so a connection that never gets that far (refused, here) can't report
             * an IP at all. Narrower than the old callback-based capture, but simpler. */
            var entry = testDb.Db.HelloRequestLogs.Single();
            Assert.AreEqual(HelloRequestOutcome.VerificationFetchFailed, entry.Outcome);
            Assert.IsNull(entry.VerificationIp,
                "A connection that was refused never produced a SpartanResponse, so there's no RemoteAddress to log.");
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
        }
    }

    [TestMethod]
    public async Task LoggingFailure_DoesNotBreakTheActualResponse()
    {
        // A disposed DbContext simulates a database that's unreachable when we go to log -
        // the real /hello response should still succeed regardless.
        var testDb = new TestHashDb();
        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance);
        testDb.Dispose();

        var serviceData = GetServiceData();
        var verifyUrl = $"https://rutabaga.example/verify/{Guid.NewGuid()}";
        var hashBackRequest = HashBackRequest.Create(serviceData.ConfigServiceHost, new Uri(verifyUrl));
        var registeredHashes = new System.Collections.Concurrent.ConcurrentDictionary<string, string>
        {
            [verifyUrl] = hashBackRequest.VerificationHash
        };
        var controller = new HelloController(serviceData, new MockHttpGetter(registeredHashes), log);
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Authorization"] = "HashBack " + hashBackRequest.AuthToken;
        controller.ControllerContext = new ControllerContext { HttpContext = ctx };

        var result = await controller.Get();
        Assert.IsInstanceOfType(result, typeof(ContentResult),
            "A logging failure must not turn a legitimate authenticated response into an error.");
    }

    /// <summary>Directly inserts a fabricated log row, bypassing LogAsync's own clock, for setting up block-threshold scenarios.</summary>
    private static void SeedFailure(TestHashDb testDb, IPAddress callerIp, DateTime requestedAt, HelloRequestOutcome outcome)
        => testDb.Db.HelloRequestLogs.Add(new HelloRequestLogRecord
        {
            RequestedAt = requestedAt,
            CallerIp = callerIp,
            Outcome = outcome
        });

    [TestMethod]
    public async Task IsCallerBlocked_OneUnderThreshold_ReturnsFalse()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var callerIp = IPAddress.Parse("203.0.113.1");
        for (int i = 0; i < HelloRequestLog.FailureThreshold - 1; i++)
            SeedFailure(testDb, callerIp, now.AddMinutes(-i), HelloRequestOutcome.WrongHash);
        await testDb.Db.SaveChangesAsync();

        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance, () => now);
        Assert.IsFalse(await log.IsCallerBlockedAsync(callerIp));
    }

    [TestMethod]
    public async Task IsCallerBlocked_AtThresholdWithinWindow_ReturnsTrue()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var callerIp = IPAddress.Parse("203.0.113.2");
        for (int i = 0; i < HelloRequestLog.FailureThreshold; i++)
            SeedFailure(testDb, callerIp, now.AddMinutes(-i), HelloRequestOutcome.WrongHash);
        await testDb.Db.SaveChangesAsync();

        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance, () => now);
        Assert.IsTrue(await log.IsCallerBlockedAsync(callerIp));
    }

    [TestMethod]
    public async Task IsCallerBlocked_FailuresOutsideLookbackWindow_DontCount()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var callerIp = IPAddress.Parse("203.0.113.3");
        var justOutsideWindow = now.Subtract(HelloRequestLog.FailureLookbackWindow).AddSeconds(-1);
        for (int i = 0; i < HelloRequestLog.FailureThreshold; i++)
            SeedFailure(testDb, callerIp, justOutsideWindow.AddMinutes(-i), HelloRequestOutcome.WrongHash);
        await testDb.Db.SaveChangesAsync();

        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance, () => now);
        Assert.IsFalse(await log.IsCallerBlockedAsync(callerIp),
            "Failures that aged out of the lookback window shouldn't count.");
    }

    [TestMethod]
    public async Task IsCallerBlocked_SuccessesAndBlockedAttemptsDontCountTowardThreshold()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var callerIp = IPAddress.Parse("203.0.113.4");
        for (int i = 0; i < HelloRequestLog.FailureThreshold * 2; i++)
            SeedFailure(testDb, callerIp, now.AddMinutes(-i),
                i % 2 == 0 ? HelloRequestOutcome.Success : HelloRequestOutcome.CallerBlocked);
        await testDb.Db.SaveChangesAsync();

        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance, () => now);
        Assert.IsFalse(await log.IsCallerBlockedAsync(callerIp),
            "Neither Success nor CallerBlocked outcomes should count toward the failure threshold - " +
            "otherwise a blocked caller's own turned-away attempts would keep the block going forever.");
    }

    [TestMethod]
    public async Task IsCallerBlocked_DifferentCallerIp_IsUnaffected()
    {
        using var testDb = new TestHashDb();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var blockedCaller = IPAddress.Parse("203.0.113.5");
        var innocentCaller = IPAddress.Parse("203.0.113.6");
        for (int i = 0; i < HelloRequestLog.FailureThreshold; i++)
            SeedFailure(testDb, blockedCaller, now.AddMinutes(-i), HelloRequestOutcome.WrongHash);
        await testDb.Db.SaveChangesAsync();

        var log = new HelloRequestLog(testDb.Db, NullLogger<HelloRequestLog>.Instance, () => now);
        Assert.IsTrue(await log.IsCallerBlockedAsync(blockedCaller));
        Assert.IsFalse(await log.IsCallerBlockedAsync(innocentCaller));
    }
}
