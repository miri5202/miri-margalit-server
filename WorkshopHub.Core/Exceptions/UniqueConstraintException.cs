namespace WorkshopHub.Core.Exceptions;

/// <summary>
/// נזרקת משכבת ה-Data כשהשמירה הפרה אינדקס ייחודי בבסיס הנתונים
/// (למשל שתי הרשמות פעילות של אותה משתתפת לאותו מפגש, או מייל שכבר קיים).
/// </summary>
public sealed class UniqueConstraintException : Exception
{
    public UniqueConstraintException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
