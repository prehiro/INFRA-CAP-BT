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

    public AuthController(AppDbContext db, TokenService tokens, SeedService seed)
    { _db = db; _tokens = tokens; _seed = seed; }

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
            return Unauthorized(new { message = "Username atau password salah" });

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var t = _tokens.Create(user, roles);
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
