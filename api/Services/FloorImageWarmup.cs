using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Loads every floor-plan image into <see cref="FloorImageCache"/> once, in the background, at startup.
///
/// WHY: the plans live in the dev SQL Server on the other side of a subnet boundary, and the first LOB
/// read of a cold plan costs ~5 s (measured: floor 31 MISS = 5172 ms, the same request afterwards =
/// 0.7 ms). Without this warm-up that five seconds lands on whoever opens the page first, inside the
/// map's image request. Paying it during boot instead makes the user-facing read a cache HIT.
///
/// The read is synchronous on purpose, matching NetworkFloorMapsController.Image: Microsoft.Data.SqlClient
/// async LOB reads are far slower on this SQL path. It runs once, off the request thread.
/// </summary>
public sealed class FloorImageWarmup(
    IServiceScopeFactory scopes,
    FloorImageCache cache,
    ILogger<FloorImageWarmup> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Never delay host startup or the first request.
        await Task.Yield();
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var generation = cache.Generation;
            var plans = db.NetworkFloorMaps.AsNoTracking()
                .Select(x => new { x.Id, x.ImageBytes, x.ImageContentType })
                .ToList();

            var warmed = 0;
            long bytes = 0;
            foreach (var plan in plans)
            {
                if (ct.IsCancellationRequested) break;
                if (plan.ImageBytes is not { Length: > 0 } data) continue;
                cache.StoreIfCurrent(plan.Id, new FloorImage(data, plan.ImageContentType ?? "application/octet-stream"), generation);
                warmed++;
                bytes += data.LongLength;
            }

            log.LogInformation("Floor plan images warmed: {Warmed}/{Total} plans, {Kilobytes} KB", warmed, plans.Count, bytes / 1024);
        }
        catch (Exception ex)
        {
            // A failed warm-up only means the first request pays the cold read, as it did before.
            log.LogWarning(ex, "Floor plan image warm-up failed");
        }
    }
}
