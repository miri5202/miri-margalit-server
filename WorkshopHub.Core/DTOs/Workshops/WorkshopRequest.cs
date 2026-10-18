using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Workshops;

/// <summary>בקשה ליצירה או לעדכון של סדנה.</summary>
public class WorkshopRequest
{
    [Required(ErrorMessage = "שם הסדנה הוא שדה חובה.")]
    [StringLength(120, MinimumLength = 3, ErrorMessage = "שם הסדנה חייב להכיל בין 3 ל-120 תווים.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "התיאור ארוך מדי (עד 2000 תווים).")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "שם המנחה הוא שדה חובה.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "שם המנחה חייב להכיל בין 2 ל-100 תווים.")]
    public string InstructorName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "יש לבחור קטגוריה.")]
    public int CategoryId { get; set; }

    /// <summary>מזהי התגיות של הסדנה (many-to-many). רשימה ריקה = בלי תגיות.</summary>
    [MaxLength(10, ErrorMessage = "אפשר לשייך עד 10 תגיות לסדנה.")]
    public List<int> TagIds { get; set; } = new();
}
