using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.Settings;

/// <summary>
/// הגדרות ה-JWT, נטענות מהקונפיגורציה (סעיף "Jwt") דרך IOptions.
/// Issuer, Audience ו-ExpiryMinutes יושבים ב-appsettings.json.
/// ה-Key הוא סוד: הוא לא נמצא בקוד ולא ב-repo, אלא ב-User Secrets (פיתוח) או במשתנה סביבה.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required(ErrorMessage =
        "Jwt:Key is missing. It is a secret and is not stored in the repository. " +
        "Set it with: dotnet user-secrets set \"Jwt:Key\" \"<random string of at least 32 characters>\" --project WorkshopHub.API")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters long (256 bits for HMAC-SHA256).")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 24 * 60)]
    public int ExpiryMinutes { get; set; } = 60;
}
