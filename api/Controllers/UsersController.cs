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
    public UsersController(AppDbContext db) => _db = db;

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
        return NoContent();
    }
}
