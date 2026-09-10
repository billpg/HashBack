using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public class HttpGetterLargeResponseTests
{
    [TestMethod]
    public async Task GetAsync_HugeResponseBody_TruncatesAtOneKilobyteAndClosesConnectionPromptly()
    {
        /* A raw server that tries to stream a huge "file", to prove the client stops
         * reading well before it all arrives and promptly closes its end - rather than
         * politely waiting for the remote to finish sending, however large that is. */
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;

        bool serverSawConnectionClosed = false;
        long serverBytesWrittenBeforeFailure = 0;
        var serverTask = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var netstr = client.GetStream();

            /* No need to read the client's request first - it's tiny and comfortably
             * fits in the OS socket buffer regardless of whether we ever read it. */
            var header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n");
            await netstr.WriteAsync(header);

            /* Keep streaming body bytes far beyond what any client should need to read -
             * up to ~12.8MB - until the client hangs up on us. */
            byte[] chunk = Encoding.ASCII.GetBytes(new string('X', 65536));
            try
            {
                for (int i = 0; i < 200; i++)
                {
                    await netstr.WriteAsync(chunk);
                    serverBytesWrittenBeforeFailure += chunk.Length;
                }
            }
            catch (IOException)
            {
                /* Expected: the client closed its end before we finished "sending the file". */
                serverSawConnectionClosed = true;
            }
        });

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var getter = new HttpGetter(new ServiceData(), new IpFilter(), new AlwaysAllowCallPermissionChecker(), TimeSpan.FromSeconds(10));
            var url = new Uri($"http://localhost:{port}/huge-file");

            var stopwatch = Stopwatch.StartNew();
            var resp = await getter.GetAsync(url, null);
            stopwatch.Stop();

            Assert.AreEqual(200, resp.StatusCode);
            Assert.IsTrue(resp.Body.Length < 1000,
                $"Body should be capped well under 1000 bytes (headers eat into that budget too), was {resp.Body.Length}.");
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"Should return almost immediately regardless of how large the remote file is, not wait for it all to arrive; took {stopwatch.Elapsed}.");

            /* Give the server task a moment to notice the closed connection, then confirm
             * it was cut off partway through - not after streaming the whole ~12.8MB. */
            await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.IsTrue(serverSawConnectionClosed,
                "The server should have observed the client close the connection instead of reading the whole response.");
            Assert.IsTrue(serverBytesWrittenBeforeFailure < 200L * 65536,
                $"The server should have been cut off before writing the full ~12.8MB; it got {serverBytesWrittenBeforeFailure} bytes through.");
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
            listener.Stop();
        }
    }
}
