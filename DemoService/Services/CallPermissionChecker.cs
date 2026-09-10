using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace DemoService.Services;

public interface ICallPermissionChecker
{
    /// <summary>
    /// True if this target has explicitly granted permission to be called, via a
    /// well-known JSON file published on its own domain - or if the target is this
    /// service's own domain (the documented "call yourself" feature, which needs no
    /// separate opt-in).
    /// </summary>
    Task<bool> IsCallPermittedAsync(Uri target);
}

/// <summary>
/// The shape of a /.well-known/demo-hashback-dev.json grant, as documented at /permit.
/// Every property but GetPermissionGrantedTo is optional - null or omitted means
/// unrestricted. CallerIP, Url, and GetsPerHour aren't enforced yet (this is the "basic
/// deserialization" step); today, a grant is honored purely on GetPermissionGrantedTo
/// matching this service's own host.
/// </summary>
internal sealed class PermitGrant
{
    /// <summary>Must equal this service's own host, so a coincidental, unrelated JSON
    /// file at the well-known path is never misread as a grant.</summary>
    public string? GetPermissionGrantedTo { get; set; }

    /// <summary>IPv4/IPv6 addresses or networks the caller (the IP invoking /hello or
    /// /call on this service) may come from. Null/omitted means any caller IP.</summary>
    public List<string>? CallerIP { get; set; }

    /// <summary>Allowed prefixes of the target URL's local part. Null/omitted means any
    /// URL on this domain.</summary>
    public List<string>? Url { get; set; }

    /// <summary>Shared quota, across the whole grant, for GETs per hour - covering both
    /// /hello fetching a verification hash and /call making an authenticated request.
    /// Null/omitted means no limit.</summary>
    public int? GetsPerHour { get; set; }
}

/// <summary>
/// Requires a target site to have explicitly opted in - via a
/// /.well-known/demo-hashback-dev.json file on its own domain - before this service will
/// make an outbound request to it. Checked directly by HttpGetter, so it covers every
/// outbound fetch this service makes: /call's caller-supplied target, and /hello's
/// caller-supplied Verify URL alike. Without this, both endpoints are effectively an open
/// relay: anyone can make this service issue an authenticated-looking request to any
/// public HTTPS domain they name, whether or not its owner wants that. Results are cached
/// in memory (not persisted - a cache miss just costs one extra, cheap, well-known fetch;
/// there's no state here worth surviving a restart) since otherwise checking permission
/// would itself double the outbound traffic to any given target.
/// </summary>
public class CallPermissionChecker : ICallPermissionChecker
{
    private readonly ServiceData data;
    private readonly Func<DateTime> utcNow;

    /// <summary>How long a granted permission is trusted before re-checking.</summary>
    public static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromDays(1);

    /// <summary>
    /// How long a refusal (no file, a fetch failure, or an explicit refusal) is cached -
    /// deliberately shorter than the positive duration, so a site that's just published
    /// its permission file doesn't have to wait long to be recognised.
    /// </summary>
    public static readonly TimeSpan NegativeCacheDuration = TimeSpan.FromHours(1);

    private readonly ConcurrentDictionary<string, (bool allowed, DateTime expiresAt)> cache = new();

    private static readonly JsonSerializerOptions GrantJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CallPermissionChecker(ServiceData data, Func<DateTime>? utcNow = null)
    {
        this.data = data;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<bool> IsCallPermittedAsync(Uri target)
    {
        /* Calling our own /hello (the "Demo-Service-Ception" feature documented at
         * /call/) is always fine - it makes no sense to require this service to publish
         * a permission file granting itself permission to call itself. */
        if (string.Equals(target.Host, data.ConfigServiceHost, StringComparison.OrdinalIgnoreCase))
            return true;

        var now = utcNow();
        if (cache.TryGetValue(target.Host, out var cached) && cached.expiresAt > now)
            return cached.allowed;

        bool allowed = await FetchPermissionAsync(target.Host);
        var cacheDuration = allowed ? PositiveCacheDuration : NegativeCacheDuration;
        cache[target.Host] = (allowed, now.Add(cacheDuration));
        return allowed;
    }

    private async Task<bool> FetchPermissionAsync(string host)
    {
        /* Any failure at all - unreachable, a 404, malformed JSON, an explicit refusal -
         * means "not permitted". This is a permission check, not a diagnostic one: it
         * fails closed, silently, rather than distinguishing why. */
        try
        {
            var wellKnownUrl = new Uri($"https://{host}/.well-known/demo-hashback-dev.json");
            var req = new billpg.SpartanHttpClient.SpartanRequest(wellKnownUrl)
                .WithTimeout(TimeSpan.FromSeconds(5))
                .WithHeader("User-Agent", "demo.hashback.dev")
                .WithHeader("Accept", "application/json");
            var resp = await req.Run();
            if (resp.StatusCode != 200)
                return false;

            var grant = JsonSerializer.Deserialize<PermitGrant>(resp.Body, GrantJsonOptions);
            return string.Equals(grant?.GetPermissionGrantedTo, data.ConfigServiceHost, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
