using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StoryPoints.Data;

namespace StoryPoints.Services;

public class CleanupService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly RoomCoordinator _coordinator;
    private readonly IOptionsMonitor<CleanupOptions> _options;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(
        IDbContextFactory<AppDbContext> dbFactory,
        RoomCoordinator coordinator,
        IOptionsMonitor<CleanupOptions> options,
        ILogger<CleanupService> logger)
    {
        _dbFactory = dbFactory;
        _coordinator = coordinator;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Cleanup cycle failed");
                }

                var interval = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RunIntervalHours));
                try { await Task.Delay(interval, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            try { await RunCycleAsync(CancellationToken.None); }
            catch (Exception ex) { _logger.LogWarning(ex, "Final cleanup flush failed"); }
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var snapshot = _coordinator.DrainActivity();
        if (snapshot.Count > 0)
        {
            var ids = snapshot.Keys.ToList();
            var rooms = await db.Rooms.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
            foreach (var room in rooms)
            {
                if (snapshot.TryGetValue(room.Id, out var ts) && ts > room.LastActivityUtc)
                {
                    room.LastActivityUtc = ts;
                }
            }
            await db.SaveChangesAsync(ct);
        }

        var inactiveDays = Math.Max(1, _options.CurrentValue.InactiveDays);
        var threshold = DateTime.UtcNow.AddDays(-inactiveDays);
        var stale = await db.Rooms.Where(r => r.LastActivityUtc < threshold).ToListAsync(ct);
        if (stale.Count == 0) return;

        db.Rooms.RemoveRange(stale);
        await db.SaveChangesAsync(ct);

        foreach (var room in stale)
        {
            _coordinator.Evict(room.Id);
        }

        _logger.LogInformation("Cleaned up {Count} room(s) inactive since {Threshold:u}", stale.Count, threshold);
    }
}
