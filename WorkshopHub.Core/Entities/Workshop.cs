namespace WorkshopHub.Core.Entities;

/// <summary>
/// סדנה – התוכן עצמו (נושא, מנחה, תיאור). הסדנה מתקיימת במפגשים (<see cref="Session"/>),
/// ולכל מפגש מכסת מקומות משלו.
/// </summary>
public class Workshop
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    /// <summary>one-to-many: מפגשי הסדנה.</summary>
    public ICollection<Session> Sessions { get; set; } = new List<Session>();

    /// <summary>many-to-many: תגיות הסדנה.</summary>
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
