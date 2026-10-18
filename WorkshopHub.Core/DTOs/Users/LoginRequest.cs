using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Users;

public class LoginRequest
{
    [Required(ErrorMessage = "כתובת מייל היא שדה חובה.")]
    [EmailAddress(ErrorMessage = "כתובת המייל אינה תקינה.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "סיסמה היא שדה חובה.")]
    public string Password { get; set; } = string.Empty;
}
