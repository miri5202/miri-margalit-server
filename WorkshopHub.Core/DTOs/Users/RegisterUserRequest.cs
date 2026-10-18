using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Users;

/// <summary>בקשת הרשמה למערכת. תפקיד המשתמש לא מגיע מהלקוח – משתמשת חדשה היא תמיד Member.</summary>
public class RegisterUserRequest
{
    [Required(ErrorMessage = "שם מלא הוא שדה חובה.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "השם חייב להכיל בין 2 ל-100 תווים.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "כתובת מייל היא שדה חובה.")]
    [EmailAddress(ErrorMessage = "כתובת המייל אינה תקינה.")]
    [StringLength(256, ErrorMessage = "כתובת המייל ארוכה מדי.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "סיסמה היא שדה חובה.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "הסיסמה חייבת להכיל בין 8 ל-100 תווים.")]
    public string Password { get; set; } = string.Empty;
}
