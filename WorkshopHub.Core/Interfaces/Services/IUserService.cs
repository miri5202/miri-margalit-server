using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Users;

namespace WorkshopHub.Core.Interfaces.Services;

public interface IUserService
{
    /// <summary>פרטי משתמש. משתמשת רגילה רואה רק את עצמה; מנהלת רואה כל משתמש.</summary>
    Task<Result<UserResponse>> GetByIdAsync(int id, CurrentUser currentUser, CancellationToken cancellationToken = default);
}
