using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using DemoService;
using DemoService.Data;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public sealed class OutboundGetLogTests
{
    [TestMethod]
    public async Task LogAsync_SplitsTargetHostFromPathAndQuery()
    {
        using var testDb = new TestHashDb();
        var log = new OutboundGetLog(testDb.Db, NullLogger<OutboundGetLog>.Instance);

        await log.LogAsync(
            IPAddress.Parse("203.0.113.9"),
            OutboundGetSource.Call,
            new Uri("https://rutabaga.example/api/hashback/xyz?foo=1"));

        var entry = testDb.Db.OutboundGetLogs.Single();
        Assert.AreEqual(IPAddress.Parse("203.0.113.9"), entry.CallerIp);
        Assert.AreEqual(OutboundGetSource.Call, entry.Source);
        Assert.AreEqual("rutabaga.example", entry.TargetHost);
        Assert.AreEqual("/api/hashback/xyz?foo=1", entry.TargetPathAndQuery);
    }

    [TestMethod]
    public async Task LogAsync_TwoRequestsToSameHost_BothCountTowardsIt()
    {
        /* The whole point of splitting TargetHost out on its own is to let a target's
         * GetsPerHour quota (see CallPermissionChecker.PermitGrant) eventually be checked
         * with a plain grouped count. */
        using var testDb = new TestHashDb();
        var log = new OutboundGetLog(testDb.Db, NullLogger<OutboundGetLog>.Instance);
        var caller = IPAddress.Parse("203.0.113.9");

        await log.LogAsync(caller, OutboundGetSource.Hello, new Uri("https://rutabaga.example/a"));
        await log.LogAsync(caller, OutboundGetSource.Call, new Uri("https://rutabaga.example/b"));
        await log.LogAsync(caller, OutboundGetSource.Call, new Uri("https://parsnip.example/c"));

        var count = testDb.Db.OutboundGetLogs.Count(e => e.TargetHost == "rutabaga.example");
        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task GetAsync_ThroughHttpGetter_WritesAMatchingLogEntry()
    {
        /* Proves HttpGetter itself calls through to the log, not just that OutboundGetLog
         * can persist a record when asked directly. */
        using var testDb = new TestHashDb();
        var log = new OutboundGetLog(testDb.Db, NullLogger<OutboundGetLog>.Instance);
        var callerIp = IPAddress.Parse("198.51.100.4");

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var getter = new HttpGetter(
                new ServiceData(), new IpFilter(), new AlwaysAllowCallPermissionChecker(), log);

            using var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var serverTask = Task.Run(async () =>
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var netstr = client.GetStream();
                var response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
                await netstr.WriteAsync(response);
            });

            var url = new Uri($"http://localhost:{port}/some/path?x=1");
            await getter.GetAsync(url, null, callerIp, OutboundGetSource.Hello);
            await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));

            var entry = testDb.Db.OutboundGetLogs.Single();
            Assert.AreEqual(callerIp, entry.CallerIp);
            Assert.AreEqual(OutboundGetSource.Hello, entry.Source);
            Assert.AreEqual("localhost", entry.TargetHost);
            Assert.AreEqual("/some/path?x=1", entry.TargetPathAndQuery);
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
        }
    }
}
