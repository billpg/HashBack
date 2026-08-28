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
        var now = utcNow();
        var existing = await db.StoredHashes.FirstOrDefaultAsync(h => h.Id == id);

        if (existing != null)
        {
            /* Still within the reuse-block window: refuse, regardless of whether the
             * existing entry is itself still retrievable. */
            if (existing.AddedAt.Add(ReuseBlockWindow) > now)
                return false;

            /* The window has passed - this id is free to reuse. Clear its old GET events
             * (cascade delete would do this too, but only on an actual row delete; we're
             * overwriting the row in place, so it needs doing explicitly) and overwrite. */
            db.HashGetEvents.RemoveRange(db.HashGetEvents.Where(e => e.HashId == id));
            existing.Hash = hash;
            existing.AddedAt = now;
            existing.AddedBy = addedBy;
            existing.GetCount = 0;
        }
        else
        {
            db.StoredHashes.Add(new StoredHashRecord
            {
                Id = id,
                Hash = hash,
                AddedAt = now,
                AddedBy = addedBy,
                GetCount = 0
            });
        }

        try
        {
            await db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException) when (existing == null)
        {
            /* Someone else concurrently inserted this same brand-new id between our read
             * and our write - the id's primary key uniqueness caught it for us. */
            return false;
        }
    }

    public async Task<StoredHashRecord?> TryGetHashAsync(Guid id, IPAddress gotBy, string requestHeaders)
    {
        var now = utcNow();
        var retrievalCutoff = now.Subtract(RetrievalWindow);

        /* Atomically claim a GET, in a single statement, so two concurrent requests can't
         * both slip through when only one slot is left under MaxGetCount. */
        int rowsUpdated = await db.StoredHashes
            .Where(h => h.Id == id && h.AddedAt > retrievalCutoff && h.GetCount < MaxGetCount)
            .ExecuteUpdateAsync(setters => setters.SetProperty(h => h.GetCount, h => h.GetCount + 1));
        if (rowsUpdated == 0)
            return null;

        db.HashGetEvents.Add(new HashGetEventRecord
        {
            HashId = id,
            GotAt = now,
            GotBy = gotBy,
            RequestHeaders = requestHeaders
        });
        await db.SaveChangesAsync();

        return await db.StoredHashes.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
    }

    public async Task<IReadOnlyList<HashGetEventRecord>> ListGetHashEventsAsync(Guid id)
        => await db.HashGetEvents
            .AsNoTracking()
            .Where(e => e.HashId == id)
            .OrderBy(e => e.GotAt)
            .ToListAsync();
}
