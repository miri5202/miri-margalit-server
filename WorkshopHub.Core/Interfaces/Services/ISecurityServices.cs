using WorkshopHub.Core.Common;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Core.Interfaces.Services;

/// <summary>גיבוב ואימות סיסמאות.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

/// <summary>הנפקת JWT למשתמש שזוהה.</summary>
public interface ITokenService
{
    AccessToken CreateToken(User user);
}
