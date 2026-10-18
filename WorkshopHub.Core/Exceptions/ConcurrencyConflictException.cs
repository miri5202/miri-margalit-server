namespace WorkshopHub.Core.Exceptions;

/// <summary>
/// נזרקת משכבת ה-Data כשהשמירה נכשלה כי השורה השתנתה מאז שנקראה (optimistic concurrency).
/// שכבת ה-Data תופסת את DbUpdateConcurrencyException של EF Core ומתרגמת אותה לחריגה הזו,
/// כדי ששכבת ה-Service תוכל לטפל בהתנגשות בלי להכיר את EF Core.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
