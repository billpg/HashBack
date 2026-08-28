using System;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DemoService.Data;

/// <summary>
/// EF Core context for the /hash endpoint's persistent store, backed by PostgreSQL. This
/// exists specifically so the state of a running instance can be inspected (and, for
/// abuse response, edited - e.g. block-lists) from a separate connection without
/// interrupting the service itself, which an in-memory store can't offer.
/// </summary>
public class HashDbContext : DbContext
{
    public HashDbContext(DbContextOptions<HashDbContext> options)
        : base(options)
    {
    }

    public DbSet<StoredHashRecord> StoredHashes => Set<StoredHashRecord>();
    public DbSet<HashGetEventRecord> HashGetEvents => Set<HashGetEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredHashRecord>(entity =>
        {
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Hash).IsRequired();
        });

        modelBuilder.Entity<HashGetEventRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId);
            entity.Property(e => e.RecordId).ValueGeneratedOnAdd();

            /* Every GET event for a given hash id is looked up together (for the /call
             * report and for cleanup), so index the foreign key. Cascade delete: once a
             * StoredHashRecord is purged, its event log goes with it. */
            entity.HasOne<StoredHashRecord>()
                .WithMany()
                .HasForeignKey(e => e.HashId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.HashId);
        });

        /* SQLite (used for fast, dependency-free tests) has no native IP address type, so
         * store it as text there. PostgreSQL's own native "inet" type is used everywhere
         * else, without needing any conversion - checking the provider name by string
         * avoids the main app needing a reference to the SQLite provider package just for
         * this test-only branch. */
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var ipConverter = new ValueConverter<IPAddress, string>(
                ip => ip.ToString(),
                s => IPAddress.Parse(s));
            modelBuilder.Entity<StoredHashRecord>().Property(h => h.AddedBy).HasConversion(ipConverter);
            modelBuilder.Entity<HashGetEventRecord>().Property(e => e.GotBy).HasConversion(ipConverter);
        }
    }
}

/// <summary>A hash previously PUT at /hash/{id}. Kept around, past its own retrieval
/// window, purely to block the same id being reused until ReuseBlockWindow has passed -
/// see HashStore.</summary>
public class StoredHashRecord
{
    public Guid Id { get; set; }
    public byte[] Hash { get; set; } = [];
    public DateTime AddedAt { get; set; }
    public IPAddress AddedBy { get; set; } = IPAddress.None;

    /// <summary>How many times this hash has been successfully retrieved via GET.</summary>
    public int GetCount { get; set; }

    public string HashAsString => Convert.ToBase64String(Hash);
}

/// <summary>A single logged GET /hash/{id} request, kept for the /call report.</summary>
public class HashGetEventRecord
{
    public long RecordId { get; set; }
    public Guid HashId { get; set; }
    public DateTime GotAt { get; set; }
    public IPAddress GotBy { get; set; } = IPAddress.None;
    public string RequestHeaders { get; set; } = "";
}
