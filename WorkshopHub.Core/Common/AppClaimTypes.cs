namespace WorkshopHub.Core.Common;

/// <summary>שמות ה-claims ב-JWT. אותם קבועים משמשים ביצירת ה-token ובקריאה שלו.</summary>
public static class AppClaimTypes
{
    public const string UserId = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
    public const string TokenId = "jti";
}
