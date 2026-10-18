using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Sessions;

/// <summary>בקשה ליצירת מפגש חדש לסדנה.</summary>
public class SessionCreateRequest
{
    /// <summary>מועד תחילת המפגש ב-UTC (למשל 2026-12-01T16:00:00Z).</summary>
    [Required(ErrorMessage = "מועד המפגש הוא שדה חובה.")]
    public DateTime? StartsAtUtc { get; set; }

    [Range(15, 600, ErrorMessage = "משך המפגש חייב להיות בין 15 ל-600 דקות.")]
    public int DurationMinutes { get; set; }

    [Required(ErrorMessage = "מיקום המפגש הוא שדה חובה.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "המיקום חייב להכיל בין 2 ל-120 תווים.")]
    public string Location { get; set; } = string.Empty;

    [Range(1, 500, ErrorMessage = "מספר המקומות חייב להיות בין 1 ל-500.")]
    public int Capacity { get; set; }
}

/// <summary>
/// בקשה לעדכון מפגש. הלקוח חייב להחזיר את ה-RowVersion שקיבל כשטען את המפגש:
/// אם המפגש השתנה מאז (למשל מישהי נרשמה), העדכון יידחה ב-409 במקום לדרוס את השינוי.
/// </summary>
public class SessionUpdateRequest : SessionCreateRequest
{
    /// <summary>ה-concurrency token כפי שהתקבל ב-GET (Base64).</summary>
    [Required(ErrorMessage = "חובה לשלוח את ה-rowVersion שהתקבל בטעינת המפגש.")]
    [MinLength(8, ErrorMessage = "rowVersion אינו תקין.")]
    [MaxLength(8, ErrorMessage = "rowVersion אינו תקין.")]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
