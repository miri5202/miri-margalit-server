using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.DTOs.Registrations;

/// <summary>בקשת הרשמה למפגש. המשתמשת הנרשמת נלקחת מה-JWT ולא מגוף הבקשה.</summary>
public class RegistrationCreateRequest
{
    /// <summary>
    /// אם המפגש מלא: true – להצטרף לרשימת ההמתנה; false (ברירת מחדל) – לקבל 409 עם הקוד session_full.
    /// </summary>
    public bool JoinWaitlistIfFull { get; set; }
}

/// <summary>סינון ו-pagination של רשימת הרשמות.</summary>
public class RegistrationQuery : PagingQuery
{
    public RegistrationStatus? Status { get; set; }
}

public class RegistrationResponse
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public int WorkshopId { get; set; }

    public string WorkshopTitle { get; set; } = string.Empty;

    public DateTime SessionStartsAtUtc { get; set; }

    public string Location { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string UserFullName { get; set; } = string.Empty;

    public RegistrationStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? PromotedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}
