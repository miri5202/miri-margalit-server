using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Settings;
using WorkshopHub.Service.Security;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Unit;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Verify_WithTheOriginalPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("Secret#123");

        Assert.True(_hasher.Verify("Secret#123", hash));
    }

    [Fact]
    public void Verify_WithAWrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("Secret#123");

        Assert.False(_hasher.Verify("secret#123", hash));
    }

    [Fact]
    public void Hash_DoesNotContainThePassword_AndDiffersBetweenCalls()
    {
        var first = _hasher.Hash("Secret#123");
        var second = _hasher.Hash("Secret#123");

        Assert.DoesNotContain("Secret#123", first);

        // salt אקראי לכל גיבוב: אותה סיסמה נותנת גיבוב שונה בכל פעם
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA256$abc$salt$hash")]
    [InlineData("PBKDF2-SHA256$100000$%%%$%%%")]
    public void Verify_WithAMalformedStoredHash_ReturnsFalseInsteadOfThrowing(string storedHash)
    {
        Assert.False(_hasher.Verify("Secret#123", storedHash));
    }
}

public class JwtTokenServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly JwtSettings _settings = new()
    {
        Issuer = "WorkshopHub.Tests",
        Audience = "WorkshopHub.Tests.Client",
        Key = "unit-test-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz",
        ExpiryMinutes = 45
    };

    private readonly User _admin = new()
    {
        Id = 42,
        FullName = "מנהלת המערכת",
        Email = "admin@example.com",
        Role = UserRole.Admin
    };

    [Fact]
    public void CreateToken_CarriesTheUserIdAndRoleAsClaims()
    {
        var service = new JwtTokenService(Options.Create(_settings), new FixedTimeProvider(Now));

        var token = service.CreateToken(_admin);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == AppClaimTypes.UserId).Value);
        Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == AppClaimTypes.Role).Value);
        Assert.Equal("admin@example.com", jwt.Claims.Single(c => c.Type == AppClaimTypes.Email).Value);
        Assert.Equal(_settings.Issuer, jwt.Issuer);
        Assert.Contains(_settings.Audience, jwt.Audiences);
        Assert.Equal(Now.AddMinutes(45), token.ExpiresAtUtc);
        Assert.Equal(Now.AddMinutes(45), jwt.ValidTo);
    }

    [Fact]
    public void CreateToken_IsSignedWithTheConfiguredKey()
    {
        // token אמיתי (עם השעון האמיתי) שנבדק בדיוק כמו שהשרת בודק אותו
        var service = new JwtTokenService(Options.Create(_settings), TimeProvider.System);
        var token = service.CreateToken(_admin);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        var principal = handler.ValidateToken(token.Value, ValidationParametersFor(_settings.Key), out _);

        Assert.Equal("42", principal.FindFirst(AppClaimTypes.UserId)?.Value);
    }

    [Fact]
    public void CreateToken_IsRejectedWhenValidatedWithADifferentKey()
    {
        // מי שאין לו את המפתח לא יכול לזייף token (למשל לשנות את התפקיד ל-Admin)
        var service = new JwtTokenService(Options.Create(_settings), TimeProvider.System);
        var token = service.CreateToken(_admin);
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        Assert.ThrowsAny<SecurityTokenException>(() =>
            handler.ValidateToken(token.Value, ValidationParametersFor("a-completely-different-key-0123456789-abcdefghij"), out _));
    }

    private TokenValidationParameters ValidationParametersFor(string key) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = _settings.Issuer,
        ValidateAudience = true,
        ValidAudience = _settings.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateLifetime = true
    };
}
