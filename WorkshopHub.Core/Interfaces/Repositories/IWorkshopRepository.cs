using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Core.Interfaces.Repositories;

public interface IWorkshopRepository
{
    /// <summary>עמוד של סדנאות לפי סינון ומיון, כולל הקטגוריה והתגיות (קריאה בלבד).</summary>
    Task<PagedResult<Workshop>> SearchAsync(WorkshopQuery query, CancellationToken cancellationToken = default);

    /// <summary>סדנה עם הקטגוריה, התגיות והמפגשים שלה (קריאה בלבד).</summary>
    Task<Workshop?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>סדנה עם התגיות שלה, במעקב של ה-DbContext (לעדכון או למחיקה).</summary>
    Task<Workshop?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>האם יש הרשמה פעילה (מאושרת או ממתינה) לאחד ממפגשי הסדנה.</summary>
    Task<bool> HasActiveRegistrationsAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(Workshop workshop, CancellationToken cancellationToken = default);

    void Remove(Workshop workshop);
}
