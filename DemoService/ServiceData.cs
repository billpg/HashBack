using System;
using System.Collections.Concurrent;

namespace DemoService;
public class ServiceData
{
    /// <summary>The hostname this instance identifies itself as - in its User-Agent,
    /// Auth-Realm, and self-referential URLs - and the value <see cref="Controllers.HelloController"/>
    /// requires the Host header to match. Overridable on the command line; see Program.cs.</summary>
    public string ConfigServiceHost { get; set; } = "demo.hashback.dev";
    public static bool AllowGetLocalhost { get; internal set; } = false;

    /// <summary>When this instance started - for reporting uptime on /wallboard. Deliberately
    /// in-memory only (not persisted), since "uptime" should reset on every restart.</summary>
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    /// <summary>Path to the SQLite database file backing the /hash store. Edit this
    /// directly (and redeploy) to move it - there's deliberately no separate config layer
    /// for a single-file setting only the operator of this specific instance would change.</summary>
    public const string DbFilePath = "hashback.db";

    private readonly ConcurrentDictionary<string, DateTime> seenUnusUntil = new();

    /// <summary>
    /// Records a HashBack request's Unus value as seen, for replay protection. Returns
    /// false if that value has already been recorded and hasn't yet expired (i.e. this is
    /// a replay). Lives here, on the singleton ServiceData, rather than on a HashBackPolicy
    /// directly, because a fresh HashBackPolicy is built for every request - a per-request
    /// object can't remember what it saw on the previous request.
    /// </summary>
    public bool TryRecordUnus(string unus, TimeSpan window)
    {
        /* Drop any entries that have aged out of the window. */
        var now = DateTime.UtcNow;
        foreach (var kvp in seenUnusUntil)
            if (kvp.Value < now)
                seenUnusUntil.TryRemove(kvp.Key, out _);

        /* Record this value, succeeding only if it wasn't already present. */
        return seenUnusUntil.TryAdd(unus, now.Add(window));
    }
}
