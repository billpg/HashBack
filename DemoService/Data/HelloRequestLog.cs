using System;
using System.Net;
using System.Threading.Tasks;
using billpg.HashBackCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DemoService.Data;

/// <summary>How a /hello request bearing an Authorization: HashBack header was resolved.</summary>
public enum HelloRequestOutcome
{
    Success,
    BadHeader,
    WrongHost,
    WrongNow,
    ReplayedUnus,
    UnknownUser,
    WrongHash,

    /// <summary>Fetching the verification hash itself failed - unreachable, timed out, bad status, TLS rejected, filtered IP, etc.</summary>
    VerificationFetchFailed,

    /// <summary>Anything not otherwise categorised, so a request is never silently unlogged.</summary>
    UnexpectedError,

    /// <summary>
    /// Turned away outright because this caller IP already has too many recent failures -
    /// see HelloRequestLog.IsCallerBlockedAsync. Deliberately excluded from that same
    /// failure count, so a blocked caller's own blocked attempts can't keep resetting the
    /// clock on themselves.
    /// </summary>
    CallerBlocked
}

/// <summary>A single logged attempt to authenticate at /hello via an Authorization: HashBack header.</summary>
public class HelloRequestLogRecord
{
    public long RecordId { get; set; }
    public DateTime RequestedAt { get; set; }
    public IPAddress CallerIp { get; set; } = IPAddress.None;

    /* The five JSON claim fields, captured whenever the header parsed far enough to have
     * them - null if it didn't (e.g. BadHeader). */
    public string? ClaimVersion { get; set; }
    public string? ClaimHost { get; set; }
    public long? ClaimNow { get; set; }
    public string? ClaimUnus { get; set; }
    public string? ClaimVerify { get; set; }

    /// <summary>
    /// The IP address actually contacted to fetch the verification hash, if resolution got
    /// that far - null for outcomes rejected before a fetch was attempted (BadHeader,
    /// WrongHost, WrongNow, ReplayedUnus), or where resolution itself failed.
    /// </summary>
    public IPAddress? VerificationIp { get; set; }

    public HelloRequestOutcome Outcome { get; set; }

    /// <summary>Free-text detail - typically the rejection exception's message - alongside the structured Outcome.</summary>
    public string? Detail { get; set; }
}

public interface IHelloRequestLog
{
    Task LogAsync(
        IPAddress callerIp,
        HashBackRequest? claim,
        IPAddress? verificationIp,
        HelloRequestOutcome outcome,
        string? detail);

    /// <summary>
    /// True if this caller IP has racked up HelloRequestLog.FailureThreshold or more
    /// non-Success attempts within the last HelloRequestLog.FailureLookbackWindow, and
    /// should therefore be turned away without attempting real work.
    /// </summary>
    Task<bool> IsCallerBlockedAsync(IPAddress callerIp);
}

public class HelloRequestLog : IHelloRequestLog
{
    private readonly HashDbContext db;
    private readonly ILogger<HelloRequestLog> logger;
    private readonly Func<DateTime> utcNow;

    /// <summary>How far back to look when counting a caller's recent failures.</summary>
    public static readonly TimeSpan FailureLookbackWindow = TimeSpan.FromHours(1);

    /// <summary>How many failures within that window trigger a block.</summary>
    public const int FailureThreshold = 5;

    public HelloRequestLog(HashDbContext db, ILogger<HelloRequestLog> logger, Func<DateTime>? utcNow = null)
    {
        this.db = db;
        this.logger = logger;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<bool> IsCallerBlockedAsync(IPAddress callerIp)
    {
        /* Best-effort, like LogAsync: if we can't reach the database to check, fail open
         * (allow the request through) rather than turn away all traffic over a transient
         * DB hiccup - a missed block is far cheaper than an outage. */
        try
        {
            var since = utcNow().Subtract(FailureLookbackWindow);
            int failureCount = await db.HelloRequestLogs
                .Where(e => e.CallerIp == callerIp
                    && e.RequestedAt >= since
                    && e.Outcome != HelloRequestOutcome.Success
                    && e.Outcome != HelloRequestOutcome.CallerBlocked)
                .CountAsync();
            return failureCount >= FailureThreshold;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to check caller IP block status; allowing the request through.");
            return false;
        }
    }

    public async Task LogAsync(
        IPAddress callerIp,
        HashBackRequest? claim,
        IPAddress? verificationIp,
        HelloRequestOutcome outcome,
        string? detail)
    {
        /* Logging is best-effort - a database hiccup here shouldn't turn an otherwise
         * legitimate response into a 500, so failures are swallowed (after being logged
         * for our own visibility) rather than propagated. */
        try
        {
            db.HelloRequestLogs.Add(new HelloRequestLogRecord
            {
                RequestedAt = utcNow(),
                CallerIp = callerIp,
                ClaimVersion = claim?.Version,
                ClaimHost = claim?.Host,
                ClaimNow = claim?.Now,
                ClaimUnus = claim?.Unus,
                ClaimVerify = claim?.Verify.ToString(),
                VerificationIp = verificationIp,
                Outcome = outcome,
                Detail = detail
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write a HelloRequestLog entry.");
        }
    }
}
