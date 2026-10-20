using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Core.Interfaces.Repositories;

public interface ISessionRepository
{
    /// <summary>עמוד של מפגשים לפי סינון ומיון, כולל הסדנה והקטגוריה (קריאה בלבד).</summary>
    Task<PagedResult<Session>> SearchAsync(SessionQuery query, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>מפגש עם הסדנה והקטגוריה שלו (קריאה בלבד).</summary>
    Task<Session?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// מפגש במעקב של ה-DbContext, לצורך שינוי. ה-RowVersion שנקרא כאן הוא זה שייבדק בזמן השמירה.
    /// </summary>
    Task<Session?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// קובע איזה RowVersion ייבדק בזמן השמירה. משמש כשהלקוח מחזיר את ה-token שקיבל כשטען את המפגש:
    /// כך העדכון נבדק מול מה שהלקוח ראה, ולא מול מה שהשרת קרא כרגע.
    /// </summary>
    void SetOriginalRowVersion(Session session, uint rowVersion);

    Task AddAsync(Session session, CancellationToken cancellationToken = default);

    void Remove(Session session);
}
