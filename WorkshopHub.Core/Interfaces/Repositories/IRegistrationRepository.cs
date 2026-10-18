using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Core.Interfaces.Repositories;

public interface IRegistrationRepository
{
    /// <summary>הרשמה עם המפגש שלה, במעקב של ה-DbContext (לביטול).</summary>
    Task<Registration?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>הרשמה עם המפגש, הסדנה והמשתמשת (קריאה בלבד).</summary>
    Task<Registration?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>האם למשתמשת יש הרשמה פעילה (מאושרת או ממתינה) למפגש.</summary>
    Task<bool> HasActiveAsync(int sessionId, int userId, CancellationToken cancellationToken = default);

    /// <summary>הממתינות הבאות בתור של המפגש, מהוותיקה לחדשה, במעקב של ה-DbContext (לקידום).</summary>
    Task<IReadOnlyList<Registration>> GetNextWaitlistedAsync(int sessionId, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// ההרשמות הפעילות של משתמשת לקבוצת מפגשים – בשאילתה אחת (ולא שאילתה לכל מפגש, כדי להימנע מ-N+1).
    /// </summary>
    Task<IReadOnlyList<Registration>> GetActiveByUserForSessionsAsync(int userId, IReadOnlyCollection<int> sessionIds, CancellationToken cancellationToken = default);

    /// <summary>עמוד מההרשמות של משתמשת, כולל המפגש והסדנה (קריאה בלבד).</summary>
    Task<PagedResult<Registration>> GetByUserAsync(int userId, RegistrationQuery query, CancellationToken cancellationToken = default);

    /// <summary>עמוד מההרשמות למפגש, כולל המשתמשות (קריאה בלבד).</summary>
    Task<PagedResult<Registration>> GetBySessionAsync(int sessionId, RegistrationQuery query, CancellationToken cancellationToken = default);

    Task AddAsync(Registration registration, CancellationToken cancellationToken = default);
}
