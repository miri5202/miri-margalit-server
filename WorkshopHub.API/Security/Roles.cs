using WorkshopHub.Core.Enums;

namespace WorkshopHub.API.Security;

/// <summary>שמות התפקידים לשימוש ב-[Authorize(Roles = ...)]. נגזרים מה-enum כדי שלא יהיו מחרוזות כפולות.</summary>
public static class Roles
{
    public const string Admin = nameof(UserRole.Admin);
    public const string Member = nameof(UserRole.Member);
}
