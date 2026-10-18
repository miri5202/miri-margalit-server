using System.ComponentModel.DataAnnotations;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.DTOs.Workshops;

/// <summary>סינון, מיון ו-pagination של רשימת הסדנאות (מגיע מה-query string).</summary>
public class WorkshopQuery : PagingQuery
{
    /// <summary>חיפוש חופשי בשם הסדנה או בשם המנחה.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public int? TagId { get; set; }

    public WorkshopSortBy SortBy { get; set; } = WorkshopSortBy.Title;

    public bool Descending { get; set; }
}
