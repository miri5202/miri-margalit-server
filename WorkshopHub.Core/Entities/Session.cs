using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.Entities;

/// <summary>
/// מפגש של סדנה – זה המשאב המוגבל שמתחרים עליו.
/// לכל מפגש יש <see cref="Capacity"/> מקומות, וכמה משתתפות יכולות לנסות לתפוס את המקום האחרון באותו רגע.
/// </summary>
public class Session
{
    public int Id { get; set; }

    public int WorkshopId { get; set; }

    public Workshop Workshop { get; set; } = null!;

    public DateTime StartsAtUtc { get; set; }

    public int DurationMinutes { get; set; }

    public string Location { get; set; } = string.Empty;

    /// <summary>מספר המקומות במפגש.</summary>
    public int Capacity { get; set; }

    /// <summary>מספר ההרשמות המאושרות. לעולם לא עולה על <see cref="Capacity"/>.</summary>
    public int SeatsTaken { get; set; }

    /// <summary>מספר הממתינות ברשימת ההמתנה.</summary>
    public int WaitlistCount { get; set; }

    /// <summary>
    /// ה-concurrency token של המפגש. SQL Server מעדכן את העמודה (rowversion) אוטומטית בכל שינוי בשורה.
    /// EF Core מוסיף את הערך שנקרא לתנאי ה-WHERE של ה-UPDATE: אם מישהו שינה את השורה בינתיים,
    /// אף שורה לא מתעדכנת והשמירה נכשלת – וכך שתי בקשות לא יכולות לתפוס את אותו מקום.
    /// כל פעולה על הרשמות המפגש (הרשמה, ביטול, הצטרפות להמתנה) משנה את שורת המפגש,
    /// ולכן כולן מוגנות על ידי אותו token.
    /// </summary>
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<Registration> Registrations { get; set; } = new List<Registration>();

    // מאפיינים מחושבים (ללא setter – EF Core לא ממפה אותם לעמודות)
    public int SeatsLeft => Capacity - SeatsTaken;

    public bool IsFull => SeatsTaken >= Capacity;
}
