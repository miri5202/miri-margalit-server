using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.DTOs.Sessions;

/// <summary>מפגש כפי שהוא מוצג בתוך פרטי הסדנה.</summary>
public class SessionSummaryResponse
{
    public int Id { get; set; }

    public DateTime StartsAtUtc { get; set; }

    public int DurationMinutes { get; set; }

    public string Location { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public int SeatsTaken { get; set; }

    public int SeatsLeft { get; set; }

    public int WaitlistCount { get; set; }
}

/// <summary>מפגש מלא, כולל פרטי הסדנה ומצב ההרשמה של המשתמשת המחוברת.</summary>
public class SessionResponse : SessionSummaryResponse
{
    public int WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public bool IsFull { get; set; }

    /// <summary>ה-concurrency token של המפגש (הערך של xmin). נדרש בעדכון מפגש.</summary>
    public uint RowVersion { get; set; }

    /// <summary>מזהה ההרשמה הפעילה של המשתמשת המחוברת למפגש הזה, אם יש.</summary>
    public int? MyRegistrationId { get; set; }

    /// <summary>סטטוס ההרשמה הפעילה של המשתמשת המחוברת (Confirmed / Waitlisted), אם יש.</summary>
    public RegistrationStatus? MyRegistrationStatus { get; set; }
}
