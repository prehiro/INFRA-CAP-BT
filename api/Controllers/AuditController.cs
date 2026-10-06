using Api.Contracts;
using Api.Data;
using Api.Domain;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Read-only audit trail. Admin only, like User Management: the trail records who did what
/// across the whole app, so a non-admin reading it would be reading everyone else's activity.
///
/// Deliberately has NO write, update or delete endpoint. The trail is written by the
/// endpoints that actually did the work (AuditService.LogAsync at each call site), so a
/// route here that could remove a row would defeat the purpose of having one.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    /// <summary>
    /// Paginated, newest first.
    ///
    /// `from`/`to` are inclusive-lower / exclusive-upper DateTimes sent by the client in
    /// local time and converted server-side to UTC here, so a day boundary in the UI lands on
    /// the same instant regardless of which zone the browser is in.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AuditPage>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        [FromQuery] string? username = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(username))
        {
            var u = username.Trim();
            q = q.Where(a => a.Username == u);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim();
            q = q.Where(a => a.Action == act);
        }

        // Compare in UTC: CreatedAt is stored UTC, and sending local DateTimes straight into
        // the query would silently shift every timestamp by the client's offset.
        if (from.HasValue) q = q.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) q = q.Where(a => a.CreatedAt < to.Value.ToUniversalTime());

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(a => a.Summary.Contains(term) || a.Target.Contains(term) || a.Username.Contains(term));
        }

        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
                          .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        // Aggregates are computed over the FILTERED set, so the summary cards describe what
        // the user is currently looking at rather than the whole table.
        //
        // Four separate CountAsync calls rather than one `GroupBy(_ => 1)` with conditional
        // aggregates inside. The grouped form is what EF Core translates worst when the query
        // already carries optional Where clauses — it produced a 500 on this endpoint with no
        // usable message. Each of these is a plain COUNT with the same predicate, which EF
        // always translates correctly, and on an indexed table four of them cost less than the
        // row materialisation above.
        var loginCount = await q.CountAsync(a => a.Action == AuditActions.Login || a.Action == AuditActions.LoginFailed);
        var failedCount = await q.CountAsync(a => !a.Success);
        var deleteCount = await q.CountAsync(a => a.Action == AuditActions.RecordDelete || a.Action == AuditActions.UserDelete);

        // Actor breakdown.
        //
        // The projection MUST be an anonymous type, not `new AuditActor(...)`. Projecting a
        // positional record makes EF fall back to client evaluation of `g.Count()` — it emits
        // `g.AsQueryable().Count()` and then throws "could not be translated", which is the 500
        // this endpoint was returning. An anonymous type is a shape EF recognises, so the whole
        // thing stays server-side as `SELECT Username, COUNT(*) ... GROUP BY Username`.
        // The mapping to AuditActor then happens in memory over at most a handful of rows.
        var actorRows = await q.GroupBy(a => a.Username)
            .Select(g => new { Username = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();
        var actors = actorRows.Select(x => new AuditActor(x.Username, x.Count)).ToList();

        return Ok(new AuditPage(
            page,
            pageSize,
            total,
            rows.Select(a => new AuditDto(
                a.Id, a.CreatedAt, a.Username, a.UserId, a.Action, a.Target, a.TargetId,
                a.Summary, a.Success, a.IpAddress)).ToList(),
            new AuditStats(total, loginCount, failedCount, deleteCount, actors)));
    }
}