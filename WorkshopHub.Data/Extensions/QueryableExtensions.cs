using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Common;

namespace WorkshopHub.Data.Extensions;

internal static class QueryableExtensions
{
    /// <summary>
    /// Pagination אמיתי מול בסיס הנתונים: COUNT אחד לסך הרשומות, ואז Skip/Take בתוך השאילתה עצמה
    /// (מתורגם ל-OFFSET ... FETCH ב-SQL). לא טוענים את כל הטבלה לזיכרון.
    /// השאילתה חייבת להגיע ממוינת – בלי ORDER BY יציב אין משמעות לעמודים.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> orderedQuery,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Max(1, pageSize);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);

        var items = await orderedQuery
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}
