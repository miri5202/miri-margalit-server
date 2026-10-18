using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Registrations;

namespace WorkshopHub.Core.Interfaces.Services;

public interface IRegistrationService
{
    /// <summary>
    /// הרשמה של המשתמשת המחוברת למפגש – הפעולה על המשאב המוגבל.
    /// מחזירה Conflict אם המפגש מלא, אם המשתמשת כבר רשומה, או אם משתתפת אחרת תפסה את המקום באותו רגע.
    /// </summary>
    Task<Result<RegistrationResponse>> RegisterAsync(int sessionId, RegistrationCreateRequest request, CurrentUser currentUser, CancellationToken cancellationToken = default);

    /// <summary>ביטול הרשמה. אם התפנה מקום, הממתינה הוותיקה ביותר מקודמת אליו.</summary>
    Task<Result> CancelAsync(int registrationId, CurrentUser currentUser, CancellationToken cancellationToken = default);

    Task<Result<RegistrationResponse>> GetByIdAsync(int registrationId, CurrentUser currentUser, CancellationToken cancellationToken = default);

    Task<PagedResult<RegistrationResponse>> GetMineAsync(CurrentUser currentUser, RegistrationQuery query, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<RegistrationResponse>>> GetBySessionAsync(int sessionId, RegistrationQuery query, CancellationToken cancellationToken = default);
}
