using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Categories;

/// <summary>בקשה ליצירה או לעדכון של קטגוריה.</summary>
public class CategoryRequest
{
    [Required(ErrorMessage = "שם הקטגוריה הוא שדה חובה.")]
    [StringLength(60, MinimumLength = 2, ErrorMessage = "שם הקטגוריה חייב להכיל בין 2 ל-60 תווים.")]
    public string Name { get; set; } = string.Empty;
}

public class CategoryResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
