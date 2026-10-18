using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.DTOs.Tags;

namespace WorkshopHub.Core.DTOs.Workshops;

/// <summary>סדנה כפי שהיא מוצגת ברשימה.</summary>
public class WorkshopSummaryResponse
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string InstructorName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public List<TagResponse> Tags { get; set; } = new();
}

/// <summary>סדנה מלאה, כולל התיאור והמפגשים שלה.</summary>
public class WorkshopDetailsResponse : WorkshopSummaryResponse
{
    public string Description { get; set; } = string.Empty;

    public List<SessionSummaryResponse> Sessions { get; set; } = new();
}
