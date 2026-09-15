using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DemoService.Data;

/// <summary>
/// Periodically deletes StoredHashRecords once they're past HashStore.ReuseBlockWindow.
/// Most ids are used exactly once and never revisited, so nothing else would ever trigger
/// their cleanup - HashStore.TryAddHashAsync only clears an old row when that same id gets
/// reused, which is the uncommon case.
/// </summary>
public class HashCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<HashCleanupService> logger;

    public HashCleanupService(IServiceScopeFactory scopeFactory, ILogger<HashCleanupService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                /* Don't let a transient DB error kill the whole background loop - log
                 * and try again next interval. */
                logger.LogWarning(ex, "HashCleanupService pass failed; will retry next interval.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HashDbContext>();

        var cutoff = DateTime.UtcNow.Subtract(HashStore.ReuseBlockWindow);
        int deleted = await db.StoredHashes
            .Where(h => h.AddedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
            logger.LogInformation("HashCleanupService purged {Count} expired hash record(s).", deleted);
    }
}
