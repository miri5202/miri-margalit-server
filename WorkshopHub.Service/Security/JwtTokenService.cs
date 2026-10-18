using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Interfaces.Services;
using WorkshopHub.Core.Settings;

namespace WorkshopHub.Service.Security;

/// <summary>
/// מנפיק JWT חתום ב-HMAC-SHA256. ה-token נושא את מזהה המשתמש ואת התפקיד שלו כ-claims,
/// והשרת מאמת את החתימה בכל בקשה – כך שאי אפשר לשנות את התפקיד בצד הלקוח.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly TimeProvider _clock;

    public JwtTokenService(IOptions<JwtSettings> options, TimeProvider clock)
    {
        _settings = options.Value;
        _clock = clock;
    }

    public AccessToken CreateToken(User user)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAtUtc = now.AddMinutes(_settings.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(AppClaimTypes.Email, user.Email),
            new(AppClaimTypes.Name, user.FullName),
            new(AppClaimTypes.Role, user.Role.ToString()),
            new(AppClaimTypes.TokenId, Guid.NewGuid().ToString("N"))
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
