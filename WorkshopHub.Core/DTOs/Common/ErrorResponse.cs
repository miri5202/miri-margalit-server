using System.Text.Json.Serialization;

namespace WorkshopHub.Core.DTOs.Common;

/// <summary>
/// מבנה השגיאה האחיד של ה-API. כל תשובת שגיאה (400, 401, 403, 404, 409, 500) חוזרת בדיוק במבנה הזה,
/// בין אם היא נוצרה ב-Controller, ב-middleware או בוולידציה של ה-ModelState.
/// </summary>
public sealed class ErrorResponse
{
    /// <summary>סטטוס ה-HTTP.</summary>
    public int Status { get; init; }

    /// <summary>קוד שגיאה יציב, למשל session_full או concurrency_conflict.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>הודעה שאפשר להציג למשתמש.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>מזהה הבקשה – אותו מזהה שמופיע בכל שורות הלוג שלה.</summary>
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>שגיאות ולידציה לפי שדה (מופיע רק בשגיאות 400 של ולידציה).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, string[]>? Errors { get; init; }
}
