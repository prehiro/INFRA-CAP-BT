using Api.Contracts;
using Api.Data;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditService _audit;
    public UsersController(AppDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> List()
    {
        var users = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username).ToListAsync();
        return Ok(users.Select(AuthController.ToDto).ToList());
    }

    [HttpGet("roles")]
    public async Task<ActionResult<List<RoleDto>>> Roles()
        => Ok(await _db.Roles.OrderBy(r => r.Name).Select(r => new RoleDto(r.Id, r.Name, r.Description)).ToListAsync());

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest req)
    {
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(req.Username)) errors["username"] = "Username is required";
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6) errors["password"] = "Password must be at least 6 characters";
        if (errors.Count > 0) return BadRequest(new { message = "Validation failed", errors });

        if (await _db.Users.AnyAsync(u => u.Username == req.Username))
            return BadRequest(new { message = "Validation failed", errors = new Dictionary<string, string> { ["username"] = "Username is already taken" } });

        var roles = await _db.Roles.Where(r => req.RoleIds.Contains(r.Id)).ToListAsync();
        var user = new Api.Domain.AppUser
        {
            Username = req.Username.Trim(),
            Email = req.Email ?? "",
            FullName = req.FullName ?? "",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            IsActive = req.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system"
        };
        foreach (var r in roles) user.UserRoles.Add(new Api.Domain.AppUserRole { Role = r });
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var created = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstAsync(u => u.Id == user.Id);
        var actor = User.Identity?.Name ?? "unknown";
        await _audit.LogAsync(
            AuditActions.UserCreate, "User Management", $"Created user \"{created.Username}\"",
            username: actor, targetId: created.Id.ToString(),
            details: new { roles = roles.Select(r => r.Name).ToArray(), isActive = created.IsActive });
        return Ok(AuthController.ToDto(created));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserDto>> Update(int id, UpdateUserRequest req)
    {
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        if (req.Email is not null) user.Email = req.Email;
        if (req.FullName is not null) user.FullName = req.FullName;
        if (req.IsActive is not null) user.IsActive = req.IsActive.Value;
        if (!string.IsNullOrEmpty(req.Password))
        {
            if (req.Password.Length < 6) return BadRequest(new { message = "Password must be at least 6 characters" });
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        }
        if (req.RoleIds is not null)
        {
            var roles = await _db.Roles.Where(r => req.RoleIds.Contains(r.Id)).ToListAsync();
            _db.UserRoles.RemoveRange(user.UserRoles);
            await _db.SaveChangesAsync();
            foreach (var r in roles) user.UserRoles.Add(new Api.Domain.AppUserRole { Role = r });
        }

        await _db.SaveChangesAsync();
        var updated = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstAsync(u => u.Id == id);
        // Flags only, never the values: an audit row that stored an email address or a role
        // change payload would duplicate exactly the credential-adjacent data this trail
        // should stay clear of. "which fields did the admin touch" is the useful question.
        await _audit.LogAsync(
            AuditActions.UserUpdate, "User Management", $"Updated user \"{updated.Username}\"",
            username: User.Identity?.Name ?? "unknown", targetId: id.ToString(),
            details: new
            {
                changed = new
                {
                    email = req.Email is not null,
                    fullName = req.FullName is not null,
                    isActive = req.IsActive,
                    password = !string.IsNullOrEmpty(req.Password),
                    roles = req.RoleIds is not null
                }
            });
        return Ok(AuthController.ToDto(updated));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (user.Username == "admin")
            return BadRequest(new { message = "User 'admin' bawaan tidak bisa dihapus" });
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(
            AuditActions.UserDelete, "User Management", $"Deleted user \"{user.Username}\"",
            username: User.Identity?.Name ?? "unknown", targetId: id.ToString());
        return NoContent();
    }
}
