namespace WorkshopHub.Core.DTOs.Users;

/// <summary>תשובת התחברות: ה-JWT, מועד התפוגה שלו ופרטי המשתמש.</summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string TokenType { get; set; } = "Bearer";

    public DateTime ExpiresAtUtc { get; set; }

    public UserResponse User { get; set; } = null!;
}
