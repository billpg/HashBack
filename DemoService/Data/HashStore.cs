using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DemoService.Data;

public interface IHashStore
{
    /// <summary>
    /// Stores a hash at the given id. Returns false - a conflict - if that id was already
    /// used within the reuse-block window, whether or not the earlier hash is still
    /// retrievable via GetHashAsync.
    /// </summary>
    Task<bool> TryAddHashAsync(Guid id, byte[] hash, IPAddress addedBy);

    /// <summary>
    /// Retrieves a hash by id, logging the request, if it exists and is still within its
    /// retrieval window and GET-count cap. Returns null otherwise - including for an id
    /// that exists but is only being kept around to block reuse.
    /// </summary>
    Task<StoredHashRecord?> TryGetHashAsync(Guid id, IPAddress gotBy, string requestHeaders);

    /// <summary>All logged GET events for a given id, for the /call report.</summary>
    Task<IReadOnlyList<HashGetEventRecord>> ListGetHashEventsAsync(Guid id);
}

public class HashStore : IHashStore
{
    private readonly HashDbContext db;
    private readonly Func<DateTime> utcNow;

    /// <summary>How long a hash stays retrievable via GET after being stored.</summary>
    public static readonly TimeSpan RetrievalWindow = TimeSpan.FromHours(1);

    /// <summary>The maximum number of times a hash may be retrieved via GET.</summary>
    public const int MaxGetCount = 5;

    /// <summary>
    /// How long an id is blocked from reuse after being stored, regardless of whether it's
    /// still within its (much shorter) RetrievalWindow. This is deliberately much longer
    /// than RetrievalWindow: retrieval stops quickly, but the id itself stays "claimed" for
    /// a lot longer, so someone can't abuse the short retrieval window as a way to keep
    /// churning fresh content onto a small, guessable set of ids.
    /// </summary>
    public static readonly TimeSpan ReuseBlockWindow = TimeSpan.FromDays(1);

    public HashStore(HashDbContext db, Func<DateTime>? utcNow = null)
    {
        this.db = db;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    public async Task<bool> TryAddHashAsync(Guid id, byte[] hash, IPAddress addedBy)
    {
        /* Don't allow any reuse of IDs if the record is still on the DB. */
        var existing = await db.StoredHashes.FirstOrDefaultAsync(h => h.Id == id);
        if (existing != null)
            return false;

        /* Attempt to add the new hash on the DB. */
        db.StoredHashes.Add(new StoredHashRecord
        {
            Id = id,
            Hash = hash,
            AddedAt = utcNow(),
            AddedBy = addedBy,
            GetCount = 0
        });        
        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException)
        {
            /* Someone else concurrently inserted this same brand-new id between our read
             * and our write - the id's primary key uniqueness caught it for us. */
            return false;
        }
    }

    public async Task<StoredHashRecord?> TryGetHashAsync(Guid id, IPAddress gotBy, string requestHeaders)
    {
        /* Atomically claim a GET, in a single statement, so two concurrent requests can't
         * both slip through when only one slot is left under MaxGetCount. This is where the
         * decision to allow the GET or not is made. Return NULL to indicate refusal. */
        var now = utcNow();
        var retrievalCutoff = now.Subtract(RetrievalWindow);
        int rowsUpdated = await db.StoredHashes
            .Where(h => h.Id == id && h.AddedAt > retrievalCutoff && h.GetCount < MaxGetCount)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.GetCount, h => h.GetCount + 1));
        if (rowsUpdated == 0)
            return null;

        /* Log the GET event. */
        db.HashGetEvents.Add(new HashGetEventRecord
        {
            HashId = id,
            GotAt = now,
            GotBy = gotBy,
            RequestHeaders = requestHeaders
        });
        await db.SaveChangesAsync();

        /* Return the now-updated hash record. */
        return await db.StoredHashes.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<IReadOnlyList<HashGetEventRecord>> ListGetHashEventsAsync(Guid id)
        => await db.HashGetEvents
            .AsNoTracking()
            .Where(e => e.HashId == id)
            .OrderBy(e => e.GotAt)
            .ToListAsync();
}
