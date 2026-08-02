using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DemoService;
public class ServiceData
{
    public string ConfigServiceHost { get; set; } = "localhost:9001";

    private readonly ConcurrentDictionary<Guid, StoredHash> hashes
        = new();

    private readonly ConcurrentDictionary<Guid, List<HashGetEvent>> hashGetEvents = new();

    public IList<HashGetEvent> ListGetHashEvents(Guid id)
        => hashGetEvents.TryGetValue(id, out var events) 
            ? events.ToList().AsReadOnly() 
            : new List<HashGetEvent>();

    public bool TryAddHash(Guid id, StoredHash hash)
        => hashes.TryAdd(id, hash);

    public StoredHash? TryGetHash(Guid id, IPAddress gotBy, string requestHeaders)
    {
        /* Remove expired hash records. */
        ClearExpired();

        /* Find the hash record by ID. */
        var hashRecord = hashes.TryGetValue(id, out var hash) ? hash : null;
        if (hashRecord == null)
            return null;

        /* Log the GET request. */
        var eventRecord = new HashGetEvent(id, DateTime.UtcNow, gotBy, requestHeaders);
        var events = hashGetEvents.AddOrUpdate(id,
            /* If the key does not exist, create a new list with the event. */
            _ => [eventRecord],
            /* If the key exists, add the event to the existing list. */
            (_, existingList) =>
            {
                existingList.Add(eventRecord);
                return existingList;
            });

        /* If the event has exhausted its GET count, remove the hash record.
         * But still return the hash record one last time. Also keep the
         * get events as the POST request handler may need them. */
        if (events.Count >= 5)
        {
            hashes.TryRemove(id, out _);           
        }

        /* Finally return the logged hash record. */
        return hashRecord;
    }

    private void ClearExpired()
    {
        /* Remove expired hash records. */
        var now = DateTime.UtcNow;
        foreach (var kvp in hashes)
            if (kvp.Value.ExpiresAt < now)
                hashes.TryRemove(kvp.Key, out _);

        /* Remove expired GET events. */
        foreach (var kvp in hashGetEvents)
        {
            var events = kvp.Value;
            events.RemoveAll(e => e.ExpiresAt < now);
            if (events.Count == 0)
            {
                hashGetEvents.TryRemove(kvp.Key, out _);
            }
        }
    }
}

public record StoredHash(IList<byte> Hash, DateTime AddedAt, IPAddress AddedBy)
{
    public string HashAsString => Convert.ToBase64String(Hash.ToArray());
    public DateTime ExpiresAt => AddedAt.AddHours(1);
}

public record class HashGetEvent(Guid Id, DateTime GotAt, IPAddress GotBy, string RequestHeaders)
{   
    internal DateTime ExpiresAt => GotAt.AddDays(1);
}