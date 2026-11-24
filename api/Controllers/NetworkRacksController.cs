using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Data;
using Api.Services;
using Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record RackInput(string? Name, string? Description, double X, double Y, int? SizeU = null);
public record RackSwitchInput(string? IpAddress, string? Hostname, string? Position, string? Model = null, string? SerialNumber = null);
public record SwitchPosition(string? IpAddress, string? Hostname, string? Position);
public record RackHudSettingsInput(string? Settings);

[ApiController]
[Route("api/network/floor-maps/{floorId:int}/racks")]
[Authorize]
public class NetworkRacksController(AppDbContext db) : ControllerBase
{
    private const int MaxNameLength = 60;
    private const int MaxModelLength = 120;
    private const int MaxDescriptionLength = 300;
    private const int MaxIpLength = 45;
    private const int MaxHostnameLength = 160;
    private const int MaxPositionLength = 100;
    private const int MaxHudSettingsLength = 4000;

    // The sizes the UI offers. Kept server-side too so a hand-made request cannot store a 3U cabinet
    // that the 3D model would then have to render.
    private static readonly int[] AllowedSizes = [19, 22, 27, 42];
    private const int DefaultSizeU = 42;

    [HttpGet]
    public async Task<IActionResult> List(int floorId, CancellationToken ct)
    {
        if (!await db.NetworkFloorMaps.AnyAsync(x => x.Id == floorId, ct))
            return NotFound(new { message = "Floor plan not found." });

        var racks = await db.NetworkRacks
            .AsNoTracking()
            .Where(x => x.FloorMapId == floorId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.X,
                x.Y,
                x.SizeU,
                x.CreatedAt,
                SwitchCount = x.Switches.Count,
                Switches = x.Switches.OrderBy(s => s.Hostname).Select(s => new
                {
                    s.Id,
                    s.IpAddress,
                    s.Hostname,
                    s.Position,
                    s.Model,
                    s.SerialNumber,
                    s.CreatedAt
                }).ToList()
            })
            .ToListAsync(ct);

        return Ok(racks);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int floorId, int id, CancellationToken ct)
    {
        var rack = await db.NetworkRacks
            .AsNoTracking()
            .Where(x => x.Id == id && x.FloorMapId == floorId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.X,
                x.Y,
                x.SizeU,
                x.CreatedAt,
                x.HudSettings,
                Switches = x.Switches.OrderBy(s => s.Hostname).Select(s => new
                {
                    s.Id,
                    s.IpAddress,
                    s.Hostname,
                    s.Position,
                    s.Model,
                    s.SerialNumber,
                    s.CreatedAt
                }).ToList()
            })
            .SingleOrDefaultAsync(ct);

        return rack is null ? NotFound() : Ok(rack);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(int floorId, RackInput input, CancellationToken ct)
    {
        if (!await db.NetworkFloorMaps.AnyAsync(x => x.Id == floorId, ct))
            return NotFound(new { message = "Floor plan not found." });

        var error = ValidateRack(input);
        if (error is not null) return BadRequest(new { message = error });

        var rack = new NetworkRack
        {
            FloorMapId = floorId,
            Name = input.Name!.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            X = input.X,
            Y = input.Y,
            SizeU = input.SizeU ?? DefaultSizeU
        };
        db.NetworkRacks.Add(rack);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { floorId, id = rack.Id }, ToDto(rack));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int floorId, int id, RackInput input, CancellationToken ct)
    {
        var rack = await db.NetworkRacks.SingleOrDefaultAsync(x => x.Id == id && x.FloorMapId == floorId, ct);
        if (rack is null) return NotFound();

        var error = ValidateRack(input);
        if (error is not null) return BadRequest(new { message = error });

        rack.Name = input.Name!.Trim();
        rack.Description = input.Description?.Trim() ?? string.Empty;
        rack.X = input.X;
        rack.Y = input.Y;
        if (input.SizeU is not null) rack.SizeU = input.SizeU.Value;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(rack));
    }

    [HttpPatch("{id:int}/position")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Move(int floorId, int id, RackInput input, CancellationToken ct)
    {
        var rack = await db.NetworkRacks.SingleOrDefaultAsync(x => x.Id == id && x.FloorMapId == floorId, ct);
        if (rack is null) return NotFound();

        if (!ValidCoordinate(input.X) || !ValidCoordinate(input.Y))
            return BadRequest(new { message = "Rack position must be within the floor plan." });

        rack.X = input.X;
        rack.Y = input.Y;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(rack));
    }

    // The 3D panel's own settings, stored per rack. Kept as an opaque JSON string: the panel owns the
    // shape, so adding a toggle there does not need another migration.
    [HttpPut("{id:int}/hud-settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveHudSettings(int floorId, int id, RackHudSettingsInput input, CancellationToken ct)
    {
        var rack = await db.NetworkRacks
            .Include(x => x.FloorMap)
            .SingleOrDefaultAsync(x => x.Id == id && x.FloorMapId == floorId, ct);
        if (rack is null) return NotFound();

        var settings = input.Settings?.Trim() ?? string.Empty;
        if (settings.Length > MaxHudSettingsLength)
            return BadRequest(new { message = $"HUD settings must be at most {MaxHudSettingsLength} characters." });

        // Read before overwriting: the register is only touched when the declared serial CHANGES, so
        // nudging the 3D view (which saves its whole settings blob every time) cannot keep stomping a
        // location somebody corrected by hand in the register.
        var previousSerial = UpsRackSync.SerialOf(rack.HudSettings);

        rack.HudSettings = settings;
        await db.SaveChangesAsync(ct);

        var serial = UpsRackSync.SerialOf(settings);
        if (serial is not null && !string.Equals(serial, previousSerial, StringComparison.OrdinalIgnoreCase))
            await UpsRackSync.SyncAsync(db, serial, UpsRackSync.LocationFor(rack.FloorMap?.Name, rack.Name), ct);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int floorId, int id, CancellationToken ct)
    {
        var rack = await db.NetworkRacks.SingleOrDefaultAsync(x => x.Id == id && x.FloorMapId == floorId, ct);
        if (rack is null) return NotFound();

        db.NetworkRacks.Remove(rack); // cascades to its switches
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // Switches within a rack
    [HttpPost("{rackId:int}/switches")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddSwitch(int floorId, int rackId, RackSwitchInput input, CancellationToken ct)
    {
        var rack = await db.NetworkRacks.SingleOrDefaultAsync(x => x.Id == rackId && x.FloorMapId == floorId, ct);
        if (rack is null) return NotFound(new { message = "Rack not found." });

        var error = ValidateSwitch(input);
        if (error is not null) return BadRequest(new { message = error });

        var sw = new NetworkSwitch
        {
            RackId = rackId,
            IpAddress = input.IpAddress!.Trim(),
            Hostname = input.Hostname?.Trim() ?? string.Empty,
            Position = input.Position?.Trim() ?? string.Empty,
            Model = input.Model!.Trim(),
            SerialNumber = NormalizeSerial(input.SerialNumber)
        };
        db.NetworkSwitches.Add(sw);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { floorId, id = rackId }, ToSwitchDto(sw));
    }

    [HttpPut("{rackId:int}/switches/{switchId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateSwitch(int floorId, int rackId, int switchId, RackSwitchInput input, CancellationToken ct)
    {
        var sw = await db.NetworkSwitches.SingleOrDefaultAsync(x => x.Id == switchId && x.RackId == rackId && x.Rack.FloorMapId == floorId, ct);
        if (sw is null) return NotFound();

        var error = ValidateSwitch(input);
        if (error is not null) return BadRequest(new { message = error });

        sw.IpAddress = input.IpAddress!.Trim();
        sw.Hostname = input.Hostname?.Trim() ?? string.Empty;
        sw.Position = input.Position?.Trim() ?? string.Empty;
        sw.Model = input.Model!.Trim();
        sw.SerialNumber = NormalizeSerial(input.SerialNumber);
        await db.SaveChangesAsync(ct);
        return Ok(ToSwitchDto(sw));
    }

    [HttpDelete("{rackId:int}/switches/{switchId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteSwitch(int floorId, int rackId, int switchId, CancellationToken ct)
    {
        var sw = await db.NetworkSwitches.SingleOrDefaultAsync(x => x.Id == switchId && x.RackId == rackId && x.Rack.FloorMapId == floorId, ct);
        if (sw is null) return NotFound();

        db.NetworkSwitches.Remove(sw);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool ValidCoordinate(double n) => double.IsFinite(n) && n >= 0 && n <= 1;

    private static string? ValidateRack(RackInput input)
    {
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
            return $"Rack name is required (maximum {MaxNameLength} characters).";
        if (input.Description?.Length > MaxDescriptionLength)
            return $"Description is too long (maximum {MaxDescriptionLength} characters).";
        if (!ValidCoordinate(input.X) || !ValidCoordinate(input.Y))
            return "Rack position must be within the floor plan.";
        if (input.SizeU is not null && !AllowedSizes.Contains(input.SizeU.Value))
            return $"Rack size must be one of {string.Join(", ", AllowedSizes)}U.";
        return null;
    }

    // Serials are typed by hand and quoted on the 3D faceplate, so they are stored the way they are
    // spoken: trimmed and upper-cased, inner spaces kept.
    private static string NormalizeSerial(string? raw)
    {
        var s = (raw ?? string.Empty).Trim().ToUpperInvariant();
        return s.Length > 32 ? s[..32] : s;
    }

    private static string? ValidateSwitch(RackSwitchInput input)
    {
        // Required, like the access point's Brand/Model: the faceplate badge and the hover card
        // both quote this string, so an empty one is a device with nothing to identify it.
        var model = input.Model?.Trim();
        if (string.IsNullOrWhiteSpace(model))
            return "Brand/Model is required - pick one from the list or type a new one.";
        if (model.Length > MaxModelLength)
            return $"Brand/Model is too long (maximum {MaxModelLength} characters).";

        var ip = input.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(ip) || ip.Length > MaxIpLength || !IPAddress.TryParse(ip, out _))
            return "Enter a valid IP address.";
        if (input.Hostname?.Length > MaxHostnameLength)
            return $"Hostname is too long (maximum {MaxHostnameLength} characters).";
        if (input.Position?.Length > MaxPositionLength)
            return $"Position is too long (maximum {MaxPositionLength} characters).";
        return null;
    }

    /// <summary>
    /// Every Brand/Model already used by a switch in any rack, most used first. Grouped here rather
    /// than in the browser: the rack dialog only holds one rack's switches, while the useful pool is
    /// every model the register has ever seen.
    /// </summary>
    /// <summary>
    /// One row per rack across every floor: how many switches sit in it and how many U they occupy.
    /// The dashboard needs both in a single call - it draws switch count as the bar and U fill as the
    /// marker - and walking floor by floor would be one request per floor for a picture of the whole
    /// building. UsedU counts the UPS reservation too, so a rack with a 3000VA unit never reads empty.
    /// </summary>
    [HttpGet("~/api/network/racks/summary")]
    [Authorize]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        // A PROJECTION, not Include(). The dashboard reads counts, so materialising whole NetworkRack and
        // NetworkSwitch entities - every column of every switch, hostname and serial and notes included -
        // only to count them made this the slowest call on the dashboard (11.4s against 13-235ms for every
        // other endpoint, repeatedly, with a 1KB response). The only per-switch field the arithmetic below
        // needs is Position, and the floor's name is one column away.
        var racks = await db.NetworkRacks
            .AsNoTracking()
            .OrderBy(x => x.FloorMap!.Name)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.FloorMapId,
                FloorName = x.FloorMap!.Name,
                x.SizeU,
                x.HudSettings,
                SwitchCount = x.Switches.Count,
                Positions = x.Switches.Select(s => s.Position).ToList()
            })
            .ToListAsync(ct);

        var rows = racks.Select(r =>
        {
            var used = new HashSet<int>();
            foreach (var position in r.Positions)
            {
                var span = ParseUnitSpan(position);
                if (span is null) continue;
                for (var u = span.Value.start; u <= span.Value.end; u++) used.Add(u);
            }
            // The UPS is a device in the rack like any other: it holds U01..U02 (3000VA) or
            // U01..U03 (5000VA), and without it a rack whose only occupant is the UPS draws 0% full.
            var ups = UpsUnits(r.HudSettings);
            for (var u = 1; u <= ups; u++) used.Add(u);

            return new
            {
                rackId = r.Id,
                rackName = r.Name,
                floorId = r.FloorMapId,
                floorName = r.FloorName ?? string.Empty,
                sizeU = r.SizeU,
                switchCount = r.SwitchCount,
                usedU = used.Count
            };
        }).ToList();

        return Ok(rows);
    }

    /// <summary>
    /// "U41" or "U41-U42" (the register writes both, with a plain or an en dash) to the inclusive unit
    /// range it covers. Mirrors parseUnitRange in Rack3DView.vue so the card and the 3D view agree on
    /// what a position means.
    /// </summary>
    private static (int start, int end)? ParseUnitSpan(string? raw)
    {
        var m = Regex.Match(raw ?? string.Empty, @"u\s*(\d+)\s*(?:[-\u2013]\s*(?:u\s*)?(\d+))?", RegexOptions.IgnoreCase);
        if (!m.Success || !int.TryParse(m.Groups[1].Value, out var a)) return null;
        var b = m.Groups[2].Success && int.TryParse(m.Groups[2].Value, out var parsed) ? parsed : a;
        if (a < 1 || b < 1) return null;
        return (Math.Min(a, b), Math.Max(a, b));
    }

    /// <summary>Units the UPS panel reserves, from the opaque hud-settings JSON the rack panel owns.</summary>
    private static int UpsUnits(string? hudSettings)
    {
        if (string.IsNullOrWhiteSpace(hudSettings)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(hudSettings);
            if (!doc.RootElement.TryGetProperty("upsModel", out var model)) return 0;
            return model.GetString() switch { "3000" => 2, "5000" => 3, _ => 0 };
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    [HttpGet("~/api/network/floor-maps/switch-values")]
    [Authorize]
    public async Task<IActionResult> SwitchValues(CancellationToken ct)
    {
        var rows = await db.NetworkSwitches
            .Where(x => x.Model != null && x.Model != "")
            .GroupBy(x => x.Model)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Value)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(rows);
    }

    private static object ToDto(NetworkRack r) => new { r.Id, r.Name, r.Description, r.X, r.Y, r.SizeU, r.HudSettings, r.CreatedAt, SwitchCount = r.Switches.Count };
    private static object ToSwitchDto(NetworkSwitch s) => new { s.Id, s.IpAddress, s.Hostname, s.Position, s.Model, s.SerialNumber, s.CreatedAt };
}