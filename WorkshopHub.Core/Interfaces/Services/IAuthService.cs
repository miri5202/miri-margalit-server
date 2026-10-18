using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Users;

namespace WorkshopHub.Core.Interfaces.Services;

public interface IAuthService
{
    /// <summary>הרשמה של משתמשת חדשה (תמיד בתפקיד Member).</summary>
    Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>התחברות: בדיקת מייל וסיסמה והנפקת JWT.</summary>
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
