using System.Text.Json;

using Api.Data;
using Api.Domain;
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
/// Letting go is the other half, and it is the half that has to be right: "saya pilih none ups atau
/// hapus sn, tapi record lama masih tetap namanya". A unit that leaves a rack must stop claiming it is
/// there, or the register quietly lies. So the rack remembers the location it displaced
/// (<see cref="NetworkRack.UpsPrevLocation"/>) and puts it back on release; when there is nothing to
/// put back, the row reads <see cref="Unassigned"/> rather than a rack it no longer sits in.
///
/// One-directional throughout. Nothing here writes back to the rack, so editing the register can never
/// move a unit out of the rack it is drawn in, and a serial the rack names but the register has never
/// heard of is not an error - a unit can be mounted before it is entered in the register.
/// </summary>
public static class UpsRackSync
{
    /// <summary>What a released row falls back to when the rack has no remembered predecessor. A blank
    /// cell reads as a rendering bug in the register, and the location field is required, so the
    /// un-linked state gets a word of its own.</summary>
    public const string Unassigned = "Unassigned";

    /// <summary>
    /// The location a rack implies. The floor is part of it because rack names are only unique within
    /// a floor (both buildings have a "C"), so a bare name would be ambiguous in the register's
    /// Location column. Mirrors the "CAP Prod B2 - Rack C" shape the register's typed values already
    /// use.
    /// </summary>
    public static string LocationFor(string? floorName, string rackName)
        => string.IsNullOrWhiteSpace(floorName) ? $"Rack {rackName}" : $"{floorName.Trim()} - Rack {rackName}";

    /// <summary>
    /// The serial a rack currently declares, or null when it declares none.
    ///
    /// "None" is not only an empty serial: the panel has an explicit no-UPS model, and picking it is
    /// HIRO's other way of letting go ("saya pilih none ups atau hapus sn"). Both have to release the
    /// row, so both read as no serial here - otherwise a 3000 -&gt; none switch with the old serial
    /// still in the box would look like nothing changed and the register would keep the rack name.
    /// </summary>
    public static string? SerialOf(string? hudSettings)
    {
        if (string.IsNullOrWhiteSpace(hudSettings)) return null;
        try
        {
            using var doc = JsonDocument.Parse(hudSettings);
            var root = doc.RootElement;

            if (root.TryGetProperty("upsModel", out var model)
                && string.Equals(model.GetString()?.Trim(), "none", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!root.TryGetProperty("upsSerial", out var sn)) return null;
            var value = sn.GetString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (JsonException)
        {
            return null; // Same tolerance as UpsUnits(): a rack's settings are not ours to validate.
        }
    }

    /// <summary>
    /// Move the rack from the serial it declared until now (<paramref name="fromSerial"/>) to the one
    /// it declares now (<paramref name="toSerial"/>), releasing the first and claiming the second.
    ///
    /// A row is only touched when it still holds this rack's own address - a location somebody typed
    /// over by hand is left exactly as it is, in both directions.
    /// </summary>
    public static async Task MoveAsync(
        AppDbContext db,
        NetworkRack rack,
        string? fromSerial,
        string? toSerial,
        string location,
        CancellationToken ct)
    {
        var changed = false;

        if (fromSerial is not null)
        {
            var released = await FindAsync(db, fromSerial, ct);
            if (released is not null && string.Equals(released.Location, location, StringComparison.Ordinal))
            {
                var restore = string.IsNullOrWhiteSpace(rack.UpsPrevLocation)
                    ? Unassigned
                    : rack.UpsPrevLocation.Trim();
                if (!string.Equals(released.Location, restore, StringComparison.Ordinal))
                {
                    released.Location = restore;
                    changed = true;
                }
            }
            rack.UpsPrevLocation = null;
            changed = true;
        }

        if (toSerial is not null)
        {
            var claimed = await FindAsync(db, toSerial, ct);
            if (claimed is not null)
            {
                // Remember what this rack is displacing, so releasing it later can put it back.
                rack.UpsPrevLocation = string.Equals(claimed.Location, location, StringComparison.Ordinal)
                    ? null
                    : claimed.Location;
                if (!string.Equals(claimed.Location, location, StringComparison.Ordinal))
                {
                    claimed.Location = location;
                    changed = true;
                }
                changed = true;
            }
            else
            {
                rack.UpsPrevLocation = null;
                changed = true;
            }
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The register row with this serial. Exact-but-case-insensitive, because the serial is typed by
    /// hand in two different panels and "as2143171426" has to find "AS2143171426".
    /// </summary>
    private static Task<UpsUnit?> FindAsync(AppDbContext db, string serial, CancellationToken ct)
        => db.UpsUnits.SingleOrDefaultAsync(x => x.SerialNumber.ToLower() == serial.Trim().ToLower(), ct);
}
