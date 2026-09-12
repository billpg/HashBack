using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DemoService.Data;

/// <summary>Which of this service's own endpoints triggered an outbound GET.</summary>
public enum OutboundGetSource
{
    Hello,
    Call
}

/// <summary>
/// A single outbound GET made via HttpGetter, on behalf of a caller hitting /hello or
/// /call. TargetHost and TargetPathAndQuery are kept as separate columns - rather than one
/// combined URL - specifically so a target's own GetsPerHour quota (see
/// CallPermissionChecker.PermitGrant) can eventually be checked with a plain count grouped
/// by host and time, without parsing URLs back out of a stored string.
/// </summary>
public class OutboundGetLogRecord
{
    public long RecordId { get; set; }
    public DateTime RequestedAt { get; set; }
    public IPAddress CallerIp { get; set; } = IPAddress.None;
    public OutboundGetSource Source { get; set; }
    public string TargetHost { get; set; } = "";
    public string TargetPathAndQuery { get; set; } = "";
}

public interface IOutboundGetLog
{
    Task LogAsync(IPAddress callerIp, OutboundGetSource source, Uri target);

    /// <summary>
    /// How many GETs a target host has received at or after "since" - for checking against
    /// that target's own declared GetsPerHour quota (see CallPermissionChecker.PermitGrant).
    /// Unlike LogAsync, this doesn't swallow its own failures - a permission check that
    /// can't be verified should be treated as a failure by its caller, not silently ignored.
    /// </summary>
    Task<int> CountRecentGetsAsync(string targetHost, DateTime since);
}

public class OutboundGetLog : IOutboundGetLog
{
    private readonly HashDbContext db;
    private readonly ILogger<OutboundGetLog> logger;
    private readonly Func<DateTime> utcNow;

    public OutboundGetLog(HashDbContext db, ILogger<OutboundGetLog> logger, Func<DateTime>? utcNow = null)
    {
        this.db = db;
        this.logger = logger;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task LogAsync(IPAddress callerIp, OutboundGetSource source, Uri target)
    {
        /* Logging is best-effort - a database hiccup here shouldn't turn an otherwise
         * legitimate GET into a failure, so failures are swallowed (after being logged for
         * our own visibility) rather than propagated. */
        try
        {
            db.OutboundGetLogs.Add(new OutboundGetLogRecord
            {
                RequestedAt = utcNow(),
                CallerIp = callerIp,
                Source = source,
                TargetHost = target.Host,
                TargetPathAndQuery = target.PathAndQuery
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write an OutboundGetLog entry.");
        }
    }

    public async Task<int> CountRecentGetsAsync(string targetHost, DateTime since)
        => await db.OutboundGetLogs.CountAsync(e => e.TargetHost == targetHost && e.RequestedAt >= since);
}
