using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Logbook-specific endpoints. Only the pieces that the generic record engine cannot
/// express: previewing the next sequential "NO" for the printed form.
/// Reading, creating, editing and deleting rows all go through the generic
/// /api/records/{entityId} endpoints, so there is a single code path for the data.
/// </summary>
[ApiController]
[Route("api/logbook/cctv")]
[Authorize]
public class CctvLogbookController : ControllerBase
{
    private readonly LogbookNumberService _numbers;
    public CctvLogbookController(LogbookNumberService numbers) => _numbers = numbers;

    /// <summary>
    /// Next "NO" in the 1/2026/001 format. Pass ?date= to number against a different
    /// year than the current one (used when back-filling a previous year, which is
    /// why the year is a parameter and not simply DateTime.Now.Year).
    /// </summary>
    [HttpGet("next-no")]
    public async Task<ActionResult<object>> NextNo([FromQuery] DateTime? date = null)
    {
        var value = await _numbers.NextCctvNumberAsync(date);
        return Ok(new { nomor = value });
    }
}