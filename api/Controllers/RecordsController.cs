using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Generic record endpoints. One set of routes serves every dynamic entity:
/// the {entityId} route segment selects which metadata-driven entity is addressed.
/// </summary>
[ApiController]
[Route("api/records/{entityId:int}")]
[Authorize]
public class RecordsController : ControllerBase
{
    private readonly DynamicRecordService _svc;
    public RecordsController(DynamicRecordService svc) => _svc = svc;

    private string Username => User.Identity?.Name ?? "unknown";

    [HttpGet]
    public async Task<ActionResult<RecordPage>> List(
        [FromRoute] int entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? lookupFieldId = null,
        [FromQuery] long? lookupValue = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = false)
        => Ok(await _svc.ListAsync(entityId, page, pageSize, search, lookupFieldId, lookupValue, sort, desc));

    [HttpGet("{recordId:long}")]
    public async Task<ActionResult<RecordDto>> Get([FromRoute] int entityId, [FromRoute] long recordId)
        => Ok(await _svc.GetAsync(entityId, recordId));

    [HttpPost]
    public async Task<ActionResult<RecordDto>> Create([FromRoute] int entityId, [FromBody] SaveRecordRequest req)
        => Ok(await _svc.CreateAsync(entityId, req, Username));

    [HttpPut("{recordId:long}")]
    public async Task<ActionResult<RecordDto>> Update(
        [FromRoute] int entityId, [FromRoute] long recordId, [FromBody] SaveRecordRequest req)
        => Ok(await _svc.UpdateAsync(entityId, recordId, req, Username));

    [HttpDelete("{recordId:long}")]
    public async Task<IActionResult> Delete([FromRoute] int entityId, [FromRoute] long recordId)
    {
        await _svc.DeleteAsync(entityId, recordId, Username);
        return NoContent();
    }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromRoute] int entityId, [FromBody] List<long> ids)
    {
        var n = await _svc.BulkDeleteAsync(entityId, ids ?? new List<long>(), Username);
        return Ok(new { deleted = n });
    }
}
