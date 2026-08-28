using System;
using System.Collections.Concurrent;

namespace DemoService;
public class ServiceData
{
    public string ConfigServiceHost { get; set; } = "localhost:9001";
    public static bool AllowGetLocalhost { get; internal set; } = false;

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
