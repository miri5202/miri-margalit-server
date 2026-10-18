namespace WorkshopHub.Core.Enums;

/// <summary>מצב ההרשמה של משתתפת למפגש.</summary>
public enum RegistrationStatus
{
    /// <summary>ההרשמה אושרה והמקום שמור.</summary>
    Confirmed = 1,

    /// <summary>המפגש היה מלא – המשתתפת ממתינה שיתפנה מקום.</summary>
    Waitlisted = 2,

    /// <summary>ההרשמה בוטלה (נשמרת לצורכי היסטוריה, לא תופסת מקום).</summary>
    Cancelled = 3
}
