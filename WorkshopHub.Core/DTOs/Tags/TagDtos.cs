using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Tags;

/// <summary>בקשה ליצירת תגית.</summary>
public class TagRequest
{
    [Required(ErrorMessage = "שם התגית הוא שדה חובה.")]
    [StringLength(40, MinimumLength = 2, ErrorMessage = "שם התגית חייב להכיל בין 2 ל-40 תווים.")]
    public string Name { get; set; } = string.Empty;
}

public class TagResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
