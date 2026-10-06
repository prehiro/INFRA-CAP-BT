using Api.Contracts;
using Api.Data;
using Api.Domain;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly TokenService _tokens;
    private readonly SeedService _seed;
    private readonly AuditService _audit;

    public AuthController(AppDbContext db, TokenService tokens, SeedService seed, AuditService audit)
    { _db = db; _tokens = tokens; _seed = seed; _audit = audit; }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { message = "Username dan password wajib diisi" });

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == req.Username);

        if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            // Recorded on BOTH failure branches. A trail that only holds successes cannot
            // answer "was anyone trying to get in?", which is the main reason to keep one.
            // No password or hash ever reaches this row.
            await _audit.LogAsync(
                AuditActions.LoginFailed,
                "Authentication",
                $"Failed sign-in for \"{req.Username}\"",
                username: req.Username,
                details: new { reason = user is null ? "no_such_user" : !user.IsActive ? "inactive" : "bad_password" },
                success: false,
                ip: HttpContext.Connection.RemoteIpAddress?.ToString());
            return Unauthorized(new { message = "Username atau password salah" });
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var t = _tokens.Create(user, roles);
        await _audit.LogAsync(
            AuditActions.Login,
            "Authentication",
            $"Signed in as {user.Username}",
            username: user.Username,
            userId: user.Id,
            details: new { roles },
            ip: HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(new LoginResponse(t.Token, t.ExpiresAt, ToDto(user)));
    }

    /// <summary>Authenticated user's own profile — used by the frontend to restore session on reload.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var id = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        return Ok(ToDto(user));
    }

    public static UserDto ToDto(AppUser u) => new(
        u.Id, u.Username, u.Email ?? "", u.FullName ?? "", u.IsActive, u.CreatedAt, u.CreatedBy ?? "",
        u.UserRoles.Select(ur => new RoleDto(ur.Role.Id, ur.Role.Name, ur.Role.Description)).ToList());
}
