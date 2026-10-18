using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.Entities;

public class User
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    /// <summary>נשמר תמיד באותיות קטנות, ויש עליו אינדקס ייחודי.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>גיבוב PBKDF2 של הסיסמה. הסיסמה עצמה לא נשמרת בשום מקום.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Member;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();
}
