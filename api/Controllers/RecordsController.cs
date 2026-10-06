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
        private readonly AuditService _audit;
        public RecordsController(DynamicRecordService svc, AuditService audit)
        { _svc = svc; _audit = audit; }

    private string Username => User.Identity?.Name ?? "unknown";

    /// <summary>
    /// Resolve the human name of an entity so the audit row says "CCTV Access Request Log"
    /// rather than "entity 7". Falls back to the raw id if the entity is gone, because a
    /// failed lookup must never stop the real work from being recorded.
    /// </summary>
    private async Task<string> TargetNameAsync(int entityId)
    {
        try
        {
            var e = await _svc.GetEntityAsync(entityId);
            return string.IsNullOrWhiteSpace(e.Name) ? $"Entity {entityId}" : e.Name;
        }
        catch
        {
            return $"Entity {entityId}";
        }
    }

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
        {
            var dto = await _svc.CreateAsync(entityId, req, Username);
            var target = await TargetNameAsync(entityId);
            await _audit.LogAsync(
                AuditActions.RecordCreate, target, $"Created a record in {target}",
                username: Username, targetId: dto.Id.ToString(),
                // Field NAMES only. Storing the values would put free text and signature data
                // into an append-only table that is read far more freely than the logbooks.
                details: new { fields = dto.Values.Keys.ToArray() });
            return Ok(dto);
        }

        [HttpPut("{recordId:long}")]
        public async Task<ActionResult<RecordDto>> Update(
            [FromRoute] int entityId, [FromRoute] long recordId, [FromBody] SaveRecordRequest req)
        {
            var dto = await _svc.UpdateAsync(entityId, recordId, req, Username);
            var target = await TargetNameAsync(entityId);
            await _audit.LogAsync(
                AuditActions.RecordUpdate, target, $"Updated record #{recordId} in {target}",
                username: Username, targetId: recordId.ToString(),
                details: new { fields = dto.Values.Keys.ToArray() });
            return Ok(dto);
        }

        [HttpDelete("{recordId:long}")]
        public async Task<IActionResult> Delete([FromRoute] int entityId, [FromRoute] long recordId)
        {
            // Resolved before the delete so the name is available even if the record had already
            // been soft-deleted and the service's own lookup would have thrown.
            var target = await TargetNameAsync(entityId);
            await _svc.DeleteAsync(entityId, recordId, Username);
            await _audit.LogAsync(
                AuditActions.RecordDelete, target, $"Deleted record #{recordId} from {target}",
                username: Username, targetId: recordId.ToString());
            return NoContent();
        }

    [HttpPost("bulk-delete")]
    public async Task<IActionResult> BulkDelete([FromRoute] int entityId, [FromBody] List<long> ids)
    {
        var n = await _svc.BulkDeleteAsync(entityId, ids ?? new List<long>(), Username);
        return Ok(new { deleted = n });
    }
}
