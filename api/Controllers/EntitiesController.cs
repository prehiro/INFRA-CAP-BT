using Api.Contracts;
using Api.Data;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/entities")]
[Authorize]
public class EntitiesController : ControllerBase
{
    private readonly DynamicRecordService _records;
    private readonly DynamicSchemaService _svc;
    private readonly AppDbContext _db;

    public EntitiesController(DynamicRecordService records, DynamicSchemaService svc, AppDbContext db)
    { _records = records; _svc = svc; _db = db; }

    [HttpGet]
    public async Task<ActionResult<List<EntityDto>>> List([FromQuery] bool all = false)
        => Ok(await _records.ListEntitiesAsync(activeOnly: !all));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EntityDto>> Get(int id)
    {
        var e = await _records.GetEntityAsync(id);
        var count = await _db.Records.CountAsync(r => r.EntityId == id && !r.IsDeleted);
        return Ok(DynamicRecordService.ToEntityDto(e, count));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EntityDto>> Create(CreateEntityRequest req)
        => Ok(await _svc.CreateEntityAsync(req));

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<EntityDto>> Update(int id, UpdateEntityRequest req)
        => Ok(await _svc.UpdateEntityAsync(id, req));

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        await _svc.DeleteEntityAsync(id);
        return NoContent();
    }
}
