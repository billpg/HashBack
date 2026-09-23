using System;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DemoService.Data;

/// <summary>
/// EF Core context for the /hash endpoint's persistent store, backed by a SQLite file (see
/// ServiceData.DbFilePath). This exists specifically so the state of a running instance can
/// be inspected from a separate connection without interrupting the service itself, which
/// an in-memory store can't offer.
/// </summary>
public class HashDbContext : DbContext
{
    public HashDbContext(DbContextOptions<HashDbContext> options)
        : base(options)
    {
    }

    public DbSet<Hash> Hashes => Set<Hash>();
    public DbSet<HashGetEvent> HashGetEvents => Set<HashGetEvent>();
    public DbSet<HelloRequest> HelloRequests => Set<HelloRequest>();
    public DbSet<OutboundGet> OutboundGets => Set<OutboundGet>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hash>(entity =>
        {
            entity.ToTable("Hash");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.HashBytes).IsRequired();
        });

        modelBuilder.Entity<HashGetEvent>(entity =>
        {
            entity.ToTable("HashGetEvent");
            entity.HasKey(e => e.Id);

            /* Every GET event for a given hash id is looked up together (for the /call
             * report and for cleanup), so index the foreign key. Cascade delete: once a
             * Hash is purged, its event log goes with it. */
            entity.HasOne<Hash>()
                .WithMany()
                .HasForeignKey(e => e.HashId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.HashId);
        });

        modelBuilder.Entity<HelloRequest>(entity =>
        {
            entity.ToTable("HelloRequest");
            entity.HasKey(e => e.Id);

            /* Stored as text (e.g. "WrongHash") rather than a bare integer, since the
             * whole point of this table is to be queried ad hoc. */
            entity.Property(e => e.Outcome).HasConversion<string>();

            /* Queried by caller IP (to spot a history of failures) and by time (recent
             * activity, retention cleanup). */
            entity.HasIndex(e => e.CallerIp);
            entity.HasIndex(e => e.RequestedAt);
        });

        modelBuilder.Entity<OutboundGet>(entity =>
        {
            entity.ToTable("OutboundGet");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Source).HasConversion<string>();

            /* Queried by target host and time - counting how many GETs a target has
             * received in the past hour, to check against its own declared GetsPerHour
             * quota (see CallPermissionChecker.PermitGrant). */
            entity.HasIndex(e => new { e.TargetHost, e.RequestedAt });
        });

        /* SQLite has no native IP address type, so every IPAddress column is stored as text. */
        var ipConverter = new ValueConverter<IPAddress, string>(
            ip => ip.ToString(),
            s => IPAddress.Parse(s));
        var nullableIpConverter = new ValueConverter<IPAddress?, string?>(
            ip => ip == null ? null : ip.ToString(),
            s => s == null ? null : IPAddress.Parse(s));
        modelBuilder.Entity<Hash>().Property(h => h.AddedBy).HasConversion(ipConverter);
        modelBuilder.Entity<HashGetEvent>().Property(e => e.GotBy).HasConversion(ipConverter);
        modelBuilder.Entity<HelloRequest>().Property(e => e.CallerIp).HasConversion(ipConverter);
        modelBuilder.Entity<HelloRequest>().Property(e => e.VerificationIp).HasConversion(nullableIpConverter);
        modelBuilder.Entity<OutboundGet>().Property(e => e.CallerIp).HasConversion(ipConverter);
    }
}

/// <summary>A hash previously PUT at /hash/{id}. Kept around, past its own retrieval
/// window, purely to block the same id being reused until ReuseBlockWindow has passed -
/// see HashStore. Id is the caller's own UUID from their Verify URL - an external key,
/// not a server-generated one, so it's used as-is rather than a fresh UUIDv7.</summary>
public class Hash
{
    public Guid Id { get; set; }
    public byte[] HashBytes { get; set; } = [];
    public DateTime AddedAt { get; set; }
    public IPAddress AddedBy { get; set; } = IPAddress.None;

    /// <summary>How many times this hash has been successfully retrieved via GET.</summary>
    public int GetCount { get; set; }

    public string HashAsString => Convert.ToBase64String(HashBytes);
}

/// <summary>A single logged GET /hash/{id} request, kept for the /call report.</summary>
public class HashGetEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid HashId { get; set; }
    public DateTime GotAt { get; set; }
    public IPAddress GotBy { get; set; } = IPAddress.None;
    public string RequestHeaders { get; set; } = "";
}
