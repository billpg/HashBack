using System;
using System.Net;
using System.Threading.Tasks;
using DemoService.Data;

namespace DemoServiceTests;

/// <summary>An IOutboundGetLog that does nothing, for tests where the log itself isn't
/// what's under test.</summary>
internal sealed class NoOpOutboundGetLog : IOutboundGetLog
{
    public Task LogAsync(IPAddress callerIp, OutboundGetSource source, Uri target) => Task.CompletedTask;

    public Task<int> CountRecentGetsAsync(string targetHost, DateTime since) => Task.FromResult(0);
}
