using System.Net;
using Api.Data;
using Api.Domain;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

public record AccessPointInput(string? Hostname, string? IpAddress, string? Position, double X, double Y, string ApType = "primary");
public record AccessPointPosition(double X, double Y);

[ApiController]
[Route("api/network/floor-maps")]
[Authorize]
public class NetworkFloorMapsController(AppDbContext db, FloorImageCache imageCache) : ControllerBase
{
    private const int MaxImageBytes = 8 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.NetworkFloorMaps
        .AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id)
        .Select(x => new { x.Id, x.Name, x.CreatedAt, AccessPointCount = x.AccessPoints.Count })
        .ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var floor = await db.NetworkFloorMaps.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name, x.CreatedAt, AccessPoints = x.AccessPoints
                .OrderBy(a => a.Hostname).Select(a => new { a.Id, a.Hostname, a.IpAddress, a.Position, a.ApType, a.X, a.Y }).ToList() })
            .SingleOrDefaultAsync(ct);
        return floor is null ? NotFound() : Ok(floor);
    }

    [HttpGet("{id:int}/image")]
    public async Task<IActionResult> Image(int id, CancellationToken ct)
    {
        var hit = imageCache.TryGet(id, out var image);
        if (!hit)
        {
            var gate = imageCache.MissGate(id);
            await gate.WaitAsync(ct);
            try
            {
                // Another request may have populated this floor while we waited.
                if (imageCache.TryGet(id, out image)) hit = true;
                else
                {
                    var generation = imageCache.Generation;
                    // Microsoft.Data.SqlClient async LOB reads are much slower on the dev SQL path.
                    // Only this cold, bounded image query uses synchronous execution.
                    var floor = db.NetworkFloorMaps.AsNoTracking().Where(x => x.Id == id)
                        .Select(x => new { x.ImageBytes, x.ImageContentType }).SingleOrDefault();
                    if (floor is null) return NotFound();
                    image = new FloorImage(floor.ImageBytes, floor.ImageContentType);
                    imageCache.StoreIfCurrent(id, image, generation);
                }
            }
            finally { gate.Release(); }
        }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Floor-Image-Cache"] = hit ? "HIT" : "MISS";
        return File(image!.Bytes, image.ContentType);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(MaxImageBytes + 65536)]
    public async Task<IActionResult> Create([FromForm] string? name, [FromForm] IFormFile? image, CancellationToken ct)
    {
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return BadRequest(new { message = "Floor name is required (maximum 120 characters)." });
        if (image is null || image.Length == 0 || image.Length > MaxImageBytes)
            return BadRequest(new { message = "Choose a floor-plan image smaller than 8 MB." });
        await using var stream = image.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var contentType = ImageType(bytes);
        if (contentType is null) return BadRequest(new { message = "Only PNG, JPEG or WebP floor-plan images are supported." });
        var cacheGeneration = imageCache.Generation;
        var floor = new NetworkFloorMap { Name = name, ImageBytes = bytes, ImageContentType = contentType };
        db.NetworkFloorMaps.Add(floor);
        await db.SaveChangesAsync(ct);
        // The uploaded bytes are already here; avoid a needless SQL round-trip on first view.
        imageCache.StoreIfCurrent(floor.Id, new FloorImage(bytes, contentType), cacheGeneration);
        return CreatedAtAction(nameof(Get), new { id = floor.Id }, new { floor.Id, floor.Name, floor.CreatedAt, AccessPointCount = 0 });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var floor = await db.NetworkFloorMaps.FindAsync([id], ct);
        if (floor is null) return NotFound();
        db.NetworkFloorMaps.Remove(floor); // cascades to its markers
        await db.SaveChangesAsync(ct);
        imageCache.Remove(id);
        return NoContent();
    }

    [HttpPost("{id:int}/access-points")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddAccessPoint(int id, AccessPointInput input, CancellationToken ct)
    {
        if (!await db.NetworkFloorMaps.AnyAsync(x => x.Id == id, ct)) return NotFound();
        var error = Validate(input);
        if (error is not null) return BadRequest(new { message = error });
        var point = new NetworkAccessPoint { FloorMapId = id };
        Copy(input, point);
        db.NetworkAccessPoints.Add(point);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id }, ToDto(point));
    }

    [HttpPut("{id:int}/access-points/{pointId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAccessPoint(int id, int pointId, AccessPointInput input, CancellationToken ct)
    {
        var point = await db.NetworkAccessPoints.SingleOrDefaultAsync(x => x.Id == pointId && x.FloorMapId == id, ct);
        if (point is null) return NotFound();
        var error = Validate(input);
        if (error is not null) return BadRequest(new { message = error });
        Copy(input, point);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(point));
    }

    [HttpPatch("{id:int}/access-points/{pointId:int}/position")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MoveAccessPoint(int id, int pointId, AccessPointPosition input, CancellationToken ct)
    {
        var point = await db.NetworkAccessPoints.SingleOrDefaultAsync(x => x.Id == pointId && x.FloorMapId == id, ct);
        if (point is null) return NotFound();
        if (!ValidCoordinate(input.X) || !ValidCoordinate(input.Y))
            return BadRequest(new { message = "Marker position must be within the floor plan." });
        point.X = input.X;
        point.Y = input.Y;
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(point));
    }

    [HttpDelete("{id:int}/access-points/{pointId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteAccessPoint(int id, int pointId, CancellationToken ct)
    {
        var point = await db.NetworkAccessPoints.SingleOrDefaultAsync(x => x.Id == pointId && x.FloorMapId == id, ct);
        if (point is null) return NotFound();
        db.NetworkAccessPoints.Remove(point);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static bool ValidCoordinate(double n) => double.IsFinite(n) && n >= 0 && n <= 1;
    private static string? Validate(AccessPointInput input)
    {
        var host = input.Hostname?.Trim();
        var ip = input.IpAddress?.Trim();
        if (string.IsNullOrWhiteSpace(host) || host.Length > 160)
            return "Hostname is required (maximum 160 characters).";
        if (string.IsNullOrWhiteSpace(ip) || ip.Length > 45 || !IPAddress.TryParse(ip, out _))
            return "Enter a valid IP address.";
        if (input.Position?.Length > 200) return "Position description is too long (maximum 200 characters).";
        if (!string.Equals(input.ApType?.Trim(), "primary", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(input.ApType?.Trim(), "bcp", StringComparison.OrdinalIgnoreCase))
            return "Choose Primary or BCP for the access point type.";
        if (!ValidCoordinate(input.X) || !ValidCoordinate(input.Y)) return "Marker position must be within the floor plan.";
        return null;
    }
    private static void Copy(AccessPointInput source, NetworkAccessPoint target)
    {
        target.Hostname = source.Hostname!.Trim();
        target.IpAddress = source.IpAddress!.Trim();
        target.Position = source.Position?.Trim() ?? string.Empty;
        target.ApType = source.ApType.Trim().ToLowerInvariant();
        target.X = source.X;
        target.Y = source.Y;
    }
    private static object ToDto(NetworkAccessPoint a) => new { a.Id, a.Hostname, a.IpAddress, a.Position, a.ApType, a.X, a.Y };
    private static string? ImageType(byte[] b)
    {
        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255) return "image/jpeg";
        if (b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }
}
