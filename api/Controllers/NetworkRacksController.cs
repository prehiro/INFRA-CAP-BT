using System.Net;
using Api.Data;
using Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record RackInput(string? Name, string? Description, double X, double Y, int? SizeU = null);
public record RackSwitchInput(string? IpAddress, string? Hostname, string? Position);
public record SwitchPosition(string? IpAddress, string? Hostname, string? Position);
public record RackHudSettingsInput(string? Settings);

[ApiController]
[Route("api/network/floor-maps/{floorId:int}/racks")]
[Authorize]
public class NetworkRacksController(AppDbContext db) : ControllerBase
{
    private const int MaxNameLength = 60;
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
        var rack = await db.NetworkRacks.SingleOrDefaultAsync(x => x.Id == id && x.FloorMapId == floorId, ct);
        if (rack is null) return NotFound();

        var settings = input.Settings?.Trim() ?? string.Empty;
        if (settings.Length > MaxHudSettingsLength)
            return BadRequest(new { message = $"HUD settings must be at most {MaxHudSettingsLength} characters." });

        rack.HudSettings = settings;
        await db.SaveChangesAsync(ct);
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
            Position = input.Position?.Trim() ?? string.Empty
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

    private static string? ValidateSwitch(RackSwitchInput input)
    {
        var ip = input.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(ip) || ip.Length > MaxIpLength || !IPAddress.TryParse(ip, out _))
            return "Enter a valid IP address.";
        if (input.Hostname?.Length > MaxHostnameLength)
            return $"Hostname is too long (maximum {MaxHostnameLength} characters).";
        if (input.Position?.Length > MaxPositionLength)
            return $"Position is too long (maximum {MaxPositionLength} characters).";
        return null;
    }

    private static object ToDto(NetworkRack r) => new { r.Id, r.Name, r.Description, r.X, r.Y, r.SizeU, r.HudSettings, r.CreatedAt, SwitchCount = r.Switches.Count };
    private static object ToSwitchDto(NetworkSwitch s) => new { s.Id, s.IpAddress, s.Hostname, s.Position, s.CreatedAt };
}