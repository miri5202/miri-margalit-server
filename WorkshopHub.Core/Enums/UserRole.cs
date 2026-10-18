namespace WorkshopHub.Core.Enums;

/// <summary>תפקיד המשתמש במערכת. נשמר ב-JWT כ-claim ונבדק בצד השרת.</summary>
public enum UserRole
{
    /// <summary>משתתפת: נרשמת למפגשים ומבטלת הרשמות של עצמה.</summary>
    Member = 1,

    /// <summary>מנהלת: מנהלת סדנאות, מפגשים, קטגוריות ותגיות, ורואה את כל ההרשמות.</summary>
    Admin = 2
}
