using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DemoService;
using DemoService.Data;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public class HttpGetterSlowDripTests
{
    [TestMethod]
    public async Task GetAsync_SlowDripWellWithinPerPacketArrival_StillTimesOutAtTheOverallLimit()
    {
        /* A server that sends a handful of bytes every 150ms - each individual packet
         * arrives comfortably before any reasonable per-read timeout - but keeps drip-
         * feeding for far longer than the configured 1 second overall timeout, to prove
         * the deadline is a single fixed point, not something a steady trickle can keep
         * resetting. */
        const int configuredTimeoutMs = 1000;
        const int dripIntervalMs = 150;
        const int dripDurationMs = 5000; // five times the configured timeout

        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var netstr = client.GetStream();

            var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n");
            var drip = Encoding.ASCII.GetBytes("1234567890");
            var dripStopwatch = Stopwatch.StartNew();
            try
            {
                await netstr.WriteAsync(header);
                while (dripStopwatch.ElapsedMilliseconds < dripDurationMs)
                {
                    await Task.Delay(dripIntervalMs);
                    await netstr.WriteAsync(drip);
                }
            }
            catch (IOException)
            {
                /* Expected once the client gives up and closes its end. */
            }
        });

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var getter = new HttpGetter(new ServiceData(), new IpFilter(), new AlwaysAllowCallPermissionChecker(), new NoOpOutboundGetLog(), TimeSpan.FromMilliseconds(configuredTimeoutMs));
            var url = new Uri($"http://localhost:{port}/slow-drip");

            var stopwatch = Stopwatch.StartNew();
            var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(
                async () => await getter.GetAsync(url, null, IPAddress.Loopback, OutboundGetSource.Hello));
            stopwatch.Stop();

            Assert.AreEqual("External URL not available.", ex.Title);
            StringAssert.Contains(ex.Message, "Timed out");

            /* Should time out at roughly the configured 1 second, not at the drip's own
             * 5 second duration - allow generous slack for CI/test-host jitter, but the
             * bug this guards against would show up as "roughly 5 seconds", not "roughly 1". */
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromMilliseconds(dripDurationMs) - TimeSpan.FromSeconds(1),
                $"Should time out at the overall {configuredTimeoutMs}ms limit despite each individual packet arriving promptly; took {stopwatch.Elapsed}.");
            Assert.IsTrue(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(configuredTimeoutMs),
                $"Shouldn't time out before the configured limit either; took {stopwatch.Elapsed}.");
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
            listener.Stop();
            await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(2)));
        }
    }
}
