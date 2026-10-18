namespace WorkshopHub.Core.Interfaces.Repositories;

/// <summary>
/// שמירה של כל השינויים שנעשו בבקשה הנוכחית כיחידה אחת (טרנזקציה אחת).
/// כל ה-repositories של אותה בקשה חולקים את אותו DbContext, ולכן קריאה אחת שומרת את כולם יחד:
/// או שהכול נשמר, או ששום דבר לא נשמר.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="Exceptions.ConcurrencyConflictException">
    /// שורה שנקראה השתנתה על ידי בקשה אחרת לפני השמירה (ה-concurrency token לא תואם).
    /// </exception>
    /// <exception cref="Exceptions.UniqueConstraintException">השמירה הפרה אינדקס ייחודי.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
