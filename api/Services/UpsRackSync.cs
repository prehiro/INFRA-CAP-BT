using System.Text.Json;

using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Keeps the UPS register's Location in step with the rack that physically carries the unit.
///
/// The rack is the source of truth: its 3D panel is where a unit is actually mounted, and that panel
/// already records which UPS (model + serial) sits in it. The register only knows a serial and a
/// location somebody typed, so when a rack declares a serial the register row for that serial is
/// pointed at the rack - HIRO: "jika ada rack UPS SN nya ada di dalam ups record, otomatis update
/// location ups list sesuai nama rack nya".
///
/// Deliberately one-directional. Nothing here writes back to the rack, so editing the register can
/// never move a unit out of the rack it is drawn in, and a serial the rack names but the register has
/// never heard of is not an error - a unit can be mounted before it is entered in the register.
/// </summary>
public static class UpsRackSync
{
    /// <summary>
    /// The location a rack implies. The floor is part of it because rack names are only unique within
    /// a floor (both buildings have a "C"), so a bare name would be ambiguous in the register's
    /// Location column. Mirrors the "CAP Prod B2 - Rack C" shape the register's typed values already
    /// use.
    /// </summary>
    public static string LocationFor(string? floorName, string rackName)
        => string.IsNullOrWhiteSpace(floorName) ? $"Rack {rackName}" : $"{floorName.Trim()} - Rack {rackName}";

    /// <summary>The serial a rack's opaque hud-settings JSON declares, or null when it declares none.</summary>
    public static string? SerialOf(string? hudSettings)
    {
        if (string.IsNullOrWhiteSpace(hudSettings)) return null;
        try
        {
            using var doc = JsonDocument.Parse(hudSettings);
            if (!doc.RootElement.TryGetProperty("upsSerial", out var sn)) return null;
            var value = sn.GetString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (JsonException)
        {
            return null; // Same tolerance as UpsUnits(): a rack's settings are not ours to validate.
        }
    }

    /// <summary>
    /// Point the register row with this serial at <paramref name="location"/>. Returns true when a row
    /// actually moved. Serial matching is exact-but-case-insensitive because the value is typed by
    /// hand in two different panels and "as2143171426" must find "AS2143171426".
    /// </summary>
    public static async Task<bool> SyncAsync(AppDbContext db, string? serial, string location, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(serial)) return false;

        var unit = await db.UpsUnits
            .SingleOrDefaultAsync(x => x.SerialNumber.ToLower() == serial.Trim().ToLower(), ct);
        if (unit is null) return false;
        if (string.Equals(unit.Location, location, StringComparison.Ordinal)) return false;

        unit.Location = location;
        await db.SaveChangesAsync(ct);
        return true;
    }
}
