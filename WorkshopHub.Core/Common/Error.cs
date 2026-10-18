namespace WorkshopHub.Core.Common;

/// <summary>סוג הכישלון העסקי. שכבת ה-API מתרגמת אותו לסטטוס HTTP.</summary>
public enum ErrorType
{
    /// <summary>400 – הבקשה לא תקינה מבחינה עסקית.</summary>
    Validation,

    /// <summary>401 – זיהוי נכשל.</summary>
    Unauthorized,

    /// <summary>403 – המשתמש מזוהה אבל אין לו הרשאה.</summary>
    Forbidden,

    /// <summary>404 – המשאב לא נמצא.</summary>
    NotFound,

    /// <summary>409 – הבקשה תקינה, אבל מתנגשת עם המצב הנוכחי של המשאב.</summary>
    Conflict
}

/// <summary>כישלון עסקי: סוג, קוד יציב שהלקוח יכול להסתמך עליו, והודעה למשתמש.</summary>
public sealed record Error(ErrorType Type, string Code, string Message);
