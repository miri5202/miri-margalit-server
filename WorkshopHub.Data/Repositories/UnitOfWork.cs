using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;

namespace WorkshopHub.Data.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // SaveChangesAsync עוטף את כל הפקודות בטרנזקציה אחת: ה-UPDATE של המפגש (עם RowVersion ב-WHERE)
            // וה-INSERT של ההרשמה מצליחים יחד או נכשלים יחד.
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // חריגה אחת ספציפית: ה-UPDATE/DELETE לא מצא שורה עם ה-RowVersion שקראנו,
            // כלומר בקשה אחרת שינתה את השורה בין הקריאה לשמירה.
            // מתרגמים אותה לחריגה של Core, כדי ששכבת ה-Service תטפל בה בלי להכיר את EF Core.
            throw new ConcurrencyConflictException(
                "The row was modified by another request between read and save.", ex);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new UniqueConstraintException("The change violates a unique index.", ex);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        // קוד השגיאה של PostgreSQL להפרת אינדקס ייחודי / אילוץ ייחודי (unique_violation)
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
