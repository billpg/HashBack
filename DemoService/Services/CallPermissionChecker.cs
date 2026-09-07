using System;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace DemoService.Services;

public interface ICallPermissionChecker
{
    /// <summary>
    /// True if the target of a /call request has explicitly granted permission to be
    /// called, via a well-known JSON file published on its own domain - or if the target
    /// is this service's own domain (the documented "call yourself" feature, which needs
    /// no separate opt-in).
    /// </summary>
    Task<bool> IsCallPermittedAsync(Uri caller);
}

/// <summary>
/// Requires a target site to have explicitly opted in - via a
/// /.well-known/demo-hashback-dev.json file on its own domain - before /call will make an
/// outbound request to it. Without this, /call is effectively an open relay: anyone can
/// make this service issue an authenticated-looking request to any public HTTPS domain
/// they name, whether or not its owner wants that. Results are cached in memory (not
/// persisted - a cache miss just costs one extra, cheap, well-known fetch; there's no
/// state here worth surviving a restart) since otherwise checking permission would itself
/// double the outbound traffic to any given target.
/// </summary>
public class CallPermissionChecker : ICallPermissionChecker
{
    private readonly IHttpGetter httpGetter;
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

    public CallPermissionChecker(IHttpGetter httpGetter, ServiceData data, Func<DateTime>? utcNow = null)
    {
        this.httpGetter = httpGetter;
        this.data = data;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<bool> IsCallPermittedAsync(Uri caller)
    {
        /* Calling our own /hello (the "Demo-Service-Ception" feature documented at
         * /call/) is always fine - it makes no sense to require this service to publish
         * a permission file granting itself permission to call itself. */
        if (string.Equals(caller.Host, data.ConfigServiceHost, StringComparison.OrdinalIgnoreCase))
            return true;

        var now = utcNow();
        if (cache.TryGetValue(caller.Host, out var cached) && cached.expiresAt > now)
            return cached.allowed;

        bool allowed = await FetchPermissionAsync(caller.Host);
        var cacheDuration = allowed ? PositiveCacheDuration : NegativeCacheDuration;
        cache[caller.Host] = (allowed, now.Add(cacheDuration));
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
            var resp = await httpGetter.GetAsync(new SimpleHttpRequest(wellKnownUrl));
            if (resp.StatusCode != 200)
                return false;

            var json = (JsonObject?)JsonNode.Parse(resp.Body);
            return json?["allow"]?.GetValue<bool>() == true;
        }
        catch
        {
            return false;
        }
    }
}
