using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DemoService;
using DemoService.Services;

namespace DemoServiceTests;

[TestClass]
public class HttpGetterTests
{
    [TestMethod]
    public async Task GetAsync_ServerAcceptsButNeverResponds_TimesOutPromptly()
    {
        /* A raw TCP listener that accepts the connection but never writes a byte back,
         * simulating a hung or malicious Verify URL that keeps the request open - the
         * denial-of-service scenario described in the HashBack specification. */
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();

        ServiceData.AllowGetLocalhost = true;
        try
        {
            var getter = new HttpGetter(new ServiceData(), new IpFilter(), new AlwaysAllowCallPermissionChecker(), TimeSpan.FromMilliseconds(200));
            var url = new Uri($"http://localhost:{port}/never-responds");

            var stopwatch = Stopwatch.StartNew();
            var ex = await Assert.ThrowsExceptionAsync<BadRequestException>(
                async () => await getter.GetAsync(url, null));
            stopwatch.Stop();

            Assert.AreEqual("External URL not available.", ex.Title);
            StringAssert.Contains(ex.Message, "Timed out");
            Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
                $"GetAsync should have thrown promptly once its 200ms timeout elapsed, took {stopwatch.Elapsed} instead.");
        }
        finally
        {
            ServiceData.AllowGetLocalhost = false;
            listener.Stop();

            /* Clean up the accepted connection, if the listener got that far. */
            if (acceptTask.IsCompletedSuccessfully)
                acceptTask.Result.Dispose();
        }
    }

    [TestMethod]
    public async Task ResolveDomain_FirstAddressRejected_FallsBackToNextAcceptableAddress()
    {
        /* A host with two DNS answers - the first one this filter won't accept, the
         * second one it will - simulating a legitimate dual-stack or multi-homed host
         * rather than assuming the resolver's first answer is always usable. */
        var rejectedAddress = IPAddress.Parse("10.1.2.3");
        var acceptedAddress = IPAddress.Parse("203.0.113.7");
        var filter = new RejectSpecificAddressesIpFilter(rejectedAddress);

        var getter = new HttpGetter(
            new ServiceData(),
            filter,
            new AlwaysAllowCallPermissionChecker(),
            dnsLookup: (host, ct) => Task.FromResult(new[] { rejectedAddress, acceptedAddress }));

        var resolved = await getter.ResolveDomainToSingleIp("rutabaga-multi-homed.example", CancellationToken.None);

        Assert.AreEqual(acceptedAddress, resolved);
        CollectionAssert.Contains(filter.Checked, rejectedAddress);
        CollectionAssert.Contains(filter.Checked, acceptedAddress);
    }

    [TestMethod]
    public async Task ResolveDomain_NoAddressAcceptable_Throws()
    {
        var rejectedAddress = IPAddress.Parse("10.1.2.3");
        var filter = new RejectSpecificAddressesIpFilter(rejectedAddress);

        var getter = new HttpGetter(
            new ServiceData(),
            filter,
            new AlwaysAllowCallPermissionChecker(),
            dnsLookup: (host, ct) => Task.FromResult(new[] { rejectedAddress }));

        await Assert.ThrowsExceptionAsync<ApplicationException>(
            async () => await getter.ResolveDomainToSingleIp("parsnip-blocked.example", CancellationToken.None));
    }

    /// <summary>A fake IIpFilter that rejects only the addresses named at construction, recording every address it was asked about.</summary>
    private class RejectSpecificAddressesIpFilter : IIpFilter
    {
        private readonly HashSet<IPAddress> rejected;
        public List<IPAddress> Checked { get; } = new();

        public RejectSpecificAddressesIpFilter(params IPAddress[] rejected)
            => this.rejected = new HashSet<IPAddress>(rejected);

        public bool IsAcceptable(IPAddress ip)
        {
            Checked.Add(ip);
            return !rejected.Contains(ip);
        }
    }
}
