using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Marketplace.Core.Entities;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Infrastructure.Services;

public interface ITokenService
{
    Task<(string token, DateTime expiresAtUtc)> CreateTokenAsync(AppUser user);
    Task<(string accessToken, DateTime expiresAtUtc, string refreshToken)> CreateSessionAsync(AppUser user, string? deviceId, string purpose = "refresh");
    Task<UserRefreshToken?> GetActiveTokenAsync(string token, string purpose = "refresh");
    Task RevokeTokenAsync(UserRefreshToken token, string? replacedByToken = null);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _opt;
    private readonly UserManager<AppUser> _userManager;
    private readonly AppDbContext _db;

    public TokenService(IOptions<JwtOptions> opt, UserManager<AppUser> userManager, AppDbContext db)
    {
        _opt = opt.Value;
        _userManager = userManager;
        _db = db;
    }

    public async Task<(string token, DateTime expiresAtUtc)> CreateTokenAsync(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("email", user.Email ?? string.Empty),
            new("displayName", user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_opt.ExpiresMinutes);
        var token = new JwtSecurityToken(_opt.Issuer, _opt.Audience, claims, expires: expires, signingCredentials: creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public async Task<(string accessToken, DateTime expiresAtUtc, string refreshToken)> CreateSessionAsync(AppUser user, string? deviceId, string purpose = "refresh")
    {
        var (accessToken, expiresAtUtc) = await CreateTokenAsync(user);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new UserRefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            Purpose = purpose,
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId!.Trim(),
            ExpiresAtUtc = purpose == "refresh" ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddMinutes(10)
        };
        _db.UserRefreshTokens.Add(entity);
        await _db.SaveChangesAsync();
        return (accessToken, expiresAtUtc, refreshToken);
    }

    public Task<UserRefreshToken?> GetActiveTokenAsync(string token, string purpose = "refresh")
    {
        return _db.UserRefreshTokens.Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == token && x.Purpose == purpose && !x.IsRevoked && x.ExpiresAtUtc > DateTime.UtcNow);
    }

    public async Task RevokeTokenAsync(UserRefreshToken token, string? replacedByToken = null)
    {
        token.IsRevoked = true;
        token.RevokedAtUtc = DateTime.UtcNow;
        token.ReplacedByToken = replacedByToken;
        await _db.SaveChangesAsync();
    }
}
