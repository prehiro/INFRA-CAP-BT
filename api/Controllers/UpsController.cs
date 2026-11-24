using Api.Data;
using Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record UpsInput(string? Brand, string? Model, string? SerialNumber, string? Location, string? AssetType);

/// <summary>The UPS register: list, add, edit, delete.</summary>
[ApiController]
[Route("api/ups")]
[Authorize]
public class UpsController(AppDbContext db) : ControllerBase
{
    private const int MaxBrandLength = 60;
    private const int MaxModelLength = 120;
    private const int MaxSerialLength = 60;
    private const int MaxLocationLength = 160;

    /// <summary>The only two values the asset-classification field accepts. A UPS is a fixed asset by
    /// default; "Non Fixed Asset" exists for units that are expensed/consumable rather than capitalised.</summary>
    private const string FixedAsset = "Fixed Asset";
    private const string NonFixedAsset = "Non Fixed Asset";

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var units = await db.UpsUnits
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Brand, x.Model, x.SerialNumber, x.Location, x.AssetType, x.CreatedAt })
            .ToListAsync(ct);
        return Ok(units);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var unit = await db.UpsUnits.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Brand, x.Model, x.SerialNumber, x.Location, x.AssetType, x.CreatedAt })
            .SingleOrDefaultAsync(ct);
        return unit is null ? NotFound() : Ok(unit);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(UpsInput input, CancellationToken ct)
    {
        var error = await Validate(input, null, ct);
        if (error is not null) return BadRequest(new { message = error });

        var unit = new UpsUnit
        {
            Brand = input.Brand!.Trim(),
            Model = input.Model!.Trim(),
            SerialNumber = input.SerialNumber!.Trim(),
            Location = input.Location!.Trim(),
            AssetType = NormalizeAssetType(input.AssetType)
        };
        db.UpsUnits.Add(unit);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = unit.Id }, ToDto(unit));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpsInput input, CancellationToken ct)
    {
        var unit = await db.UpsUnits.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (unit is null) return NotFound(new { message = "UPS not found." });

        var error = await Validate(input, id, ct);
        if (error is not null) return BadRequest(new { message = error });

        unit.Brand = input.Brand!.Trim();
        unit.Model = input.Model!.Trim();
        unit.SerialNumber = input.SerialNumber!.Trim();
        unit.Location = input.Location!.Trim();
        unit.AssetType = NormalizeAssetType(input.AssetType);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(unit));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var unit = await db.UpsUnits.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (unit is null) return NotFound(new { message = "UPS not found." });
        db.UpsUnits.Remove(unit);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<string?> Validate(UpsInput input, int? editingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Brand)) return "Brand is required.";
        if (input.Brand.Trim().Length > MaxBrandLength) return $"Brand is too long (maximum {MaxBrandLength} characters).";

        if (string.IsNullOrWhiteSpace(input.Model)) return "Model is required.";
        if (input.Model.Trim().Length > MaxModelLength) return $"Model is too long (maximum {MaxModelLength} characters).";

        if (string.IsNullOrWhiteSpace(input.SerialNumber)) return "Serial number is required.";
        if (input.SerialNumber.Trim().Length > MaxSerialLength) return $"Serial number is too long (maximum {MaxSerialLength} characters).";

        if (string.IsNullOrWhiteSpace(input.Location)) return "Location is required.";
        if (input.Location.Trim().Length > MaxLocationLength) return $"Location is too long (maximum {MaxLocationLength} characters).";

        // Anything other than the two known values is a typo from a caller that is not our form, so it
        // is refused rather than stored - a third spelling would silently split every filter on this.
        if (!string.IsNullOrWhiteSpace(input.AssetType)
            && !string.Equals(input.AssetType.Trim(), FixedAsset, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(input.AssetType.Trim(), NonFixedAsset, StringComparison.OrdinalIgnoreCase))
            return $"Asset type must be either \"{FixedAsset}\" or \"{NonFixedAsset}\".";

        // The serial is the sheet's natural key: two rows for one unit would let an edit silently
        // fork the register, so a duplicate is a friendly refusal rather than a database error.
        var serial = input.SerialNumber.Trim();
        var duplicate = await db.UpsUnits.AnyAsync(x => x.SerialNumber == serial && x.Id != (editingId ?? 0), ct);
        if (duplicate) return $"Serial number \"{serial}\" is already registered.";

        return null;
    }

    private static object ToDto(UpsUnit x) => new
    {
        x.Id,
        x.Brand,
        x.Model,
        x.SerialNumber,
        x.Location,
        x.AssetType,
        x.CreatedAt
    };

    /// <summary>Missing means the default; a known value is canonicalised so the stored spelling never
    /// drifts by case or spacing.</summary>
    private static string NormalizeAssetType(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return FixedAsset;
        return string.Equals(trimmed, NonFixedAsset, StringComparison.OrdinalIgnoreCase) ? NonFixedAsset : FixedAsset;
    }
}
