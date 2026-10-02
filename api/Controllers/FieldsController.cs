using Api.Contracts;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/fields")]
[Authorize(Roles = "Admin")]
public class FieldsController : ControllerBase
{
    private readonly DynamicSchemaService _svc;
    public FieldsController(DynamicSchemaService svc) => _svc = svc;

    [HttpPost("entity/{entityId:int}")]
    public async Task<ActionResult<FieldDto>> Add(int entityId, CreateFieldRequest req)
        => Ok(await _svc.AddFieldAsync(entityId, req));

    [HttpPut("{fieldId:int}")]
    public async Task<ActionResult<FieldDto>> Update(int fieldId, UpdateFieldRequest req)
        => Ok(await _svc.UpdateFieldAsync(fieldId, req));

    [HttpDelete("{fieldId:int}")]
    public async Task<IActionResult> Delete(int fieldId)
    {
        await _svc.DeleteFieldAsync(fieldId);
        return NoContent();
    }
}
