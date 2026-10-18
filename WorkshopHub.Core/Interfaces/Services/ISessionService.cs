using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Sessions;

namespace WorkshopHub.Core.Interfaces.Services;

public interface ISessionService
{
    /// <param name="currentUser">המשתמשת המחוברת, או null לגולשת אנונימית. משמש לסימון ההרשמות שלה ברשימה.</param>
    Task<PagedResult<SessionResponse>> SearchAsync(SessionQuery query, CurrentUser? currentUser, CancellationToken cancellationToken = default);

    Task<Result<SessionResponse>> GetByIdAsync(int id, CurrentUser? currentUser, CancellationToken cancellationToken = default);

    Task<Result<SessionResponse>> CreateAsync(int workshopId, SessionCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>עדכון מפגש. נדחה ב-Conflict אם ה-RowVersion שהלקוח שלח כבר לא עדכני.</summary>
    Task<Result<SessionResponse>> UpdateAsync(int id, SessionUpdateRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
