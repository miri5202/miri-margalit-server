using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.Entities;

/// <summary>הרשמה של משתתפת למפגש.</summary>
public class Registration
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public Session Session { get; set; } = null!;

    public int UserId { get; set; }

    public User User { get; set; } = null!;

    public RegistrationStatus Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    /// <summary>מתי ההרשמה עברה מרשימת ההמתנה למקום מאושר (אם עברה).</summary>
    public DateTime? PromotedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public bool IsActive => Status != RegistrationStatus.Cancelled;
}
