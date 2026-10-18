using System.ComponentModel.DataAnnotations;

namespace WorkshopHub.Core.DTOs.Common;

/// <summary>פרמטרי pagination שמגיעים מה-query string. משמש בסיס לכל שאילתות הרשימה.</summary>
public class PagingQuery
{
    public const int MaxPageSize = 100;

    /// <summary>מספר העמוד, מתחיל מ-1.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "מספר העמוד חייב להיות 1 ומעלה.")]
    public int Page { get; set; } = 1;

    /// <summary>מספר הרשומות בעמוד (1 עד 100).</summary>
    [Range(1, MaxPageSize, ErrorMessage = "גודל העמוד חייב להיות בין 1 ל-100.")]
    public int PageSize { get; set; } = 10;
}
