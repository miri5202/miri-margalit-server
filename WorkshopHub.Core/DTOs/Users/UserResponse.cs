using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.DTOs.Users;

/// <summary>פרטי משתמש שחוזרים ללקוח. שימי לב: אין כאן PasswordHash – הישות עצמה לא יוצאת מה-API.</summary>
public class UserResponse
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
