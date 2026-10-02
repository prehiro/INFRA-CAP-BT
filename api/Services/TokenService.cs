using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Api.Services;

public class JwtOptions
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpireHours { get; set; } = 8;
}

public class TokenResult
{
    public required string Token { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public class TokenService
{
    private readonly JwtOptions _opt;
    public TokenService(JwtOptions opt) => _opt = opt;

    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(_opt.Key));

    public TokenResult Create(AppUser user, IEnumerable<string> roles)
    {
        var expires = DateTime.UtcNow.AddHours(_opt.ExpireHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("fullName", user.FullName ?? string.Empty),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var creds = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer, audience: _opt.Audience,
            claims: claims, notBefore: DateTime.UtcNow, expires: expires,
            signingCredentials: creds);

        return new TokenResult
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            ExpiresAt = expires
        };
    }
}
