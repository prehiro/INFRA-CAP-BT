using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Keeps the reference tables the admin registers read out of the SQL Server's cold path.
///
/// WHY THIS EXISTS
/// The dev/office SQL Server is remote. The first read of a table after a quiet period costs seconds
/// there, not milliseconds - measured on this project: GET /api/users at 4798 ms while GET
/// /api/users/roles, fired in the same instant against the same connection pool, returned in 12.6 ms.
/// The difference is the table, not the code: the users query is the first one to touch Users/UserRoles
/// after the idle gap, so it pays for the read and every later request is fast.
///
/// That turns "open the user page" into a coin flip on how long the machine has been idle, which is
/// exactly what it looks like from the browser: the page itself is instant, the data behind it is not.
///
/// WHAT IT DOES
/// One cheap aggregate per minute against the tables the pages read - the reference tables the admin
/// registers use, plus the dashboard's own (network floor maps, access points, racks, switches, UPS units
/// and the metadata-driven Records/RecordValues). It stores nothing and changes no data, so there is no
/// cache to invalidate and no stale state possible - it only keeps the pages resident.
/// Failures are swallowed: a warm-up that cannot reach SQL must never take the API down with it.
/// </summary>
public sealed class SqlWarmup : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SqlWarmup> _log;

    public SqlWarmup(IServiceScopeFactory scopes, ILogger<SqlWarmup> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the host finish binding and the image warm-up run first; neither wants to queue behind us.
        try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Deliberately the lightest shape that still touches the tables' data pages.
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [Users]").FirstOrDefaultAsync(stoppingToken);
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [Roles]").FirstOrDefaultAsync(stoppingToken);

                // The DASHBOARD's tables, for the same reason - and it was this page that reported it: a
                // refresh painted the shell instantly, then one card sat there for 4.8 s while its
                // neighbours were already drawn (measured in the browser: every chart's data at 0.5-0.8 s
                // except the access-point card at 5.3 s, with GET /api/network/floor-maps at 4832 ms).
                // Warming Users/Roles alone does nothing for it, because the cold read is per TABLE: a
                // card is late by however long its own table has been idle, which is exactly "kadang lama,
                // kadang cepat" from the browser.
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [NetworkFloorMaps]").FirstOrDefaultAsync(stoppingToken);
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [NetworkAccessPoints]").FirstOrDefaultAsync(stoppingToken);
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [NetworkRacks]").FirstOrDefaultAsync(stoppingToken);
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [NetworkSwitches]").FirstOrDefaultAsync(stoppingToken);
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [UpsUnits]").FirstOrDefaultAsync(stoppingToken);
                // The metadata-driven tables behind every PC card (chassis / model distribution).
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [Records]").FirstOrDefaultAsync(stoppingToken);
                await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM [RecordValues]").FirstOrDefaultAsync(stoppingToken);

                _log.LogInformation("Reference tables kept warm ({At:HH:mm:ss})", TimeProvider.System.GetUtcNow().LocalDateTime);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Once per minute at most; a broken DB should be visible but must not be fatal.
                _log.LogWarning(ex, "Reference-table warm-up failed");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}