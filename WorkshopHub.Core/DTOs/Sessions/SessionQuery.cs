using System.ComponentModel.DataAnnotations;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.DTOs.Sessions;

/// <summary>סינון, מיון ו-pagination של רשימת המפגשים (מגיע מה-query string).</summary>
public class SessionQuery : PagingQuery
{
    /// <summary>חיפוש חופשי בשם הסדנה או בשם המנחה.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    public int? WorkshopId { get; set; }

    public int? CategoryId { get; set; }

    /// <summary>רק מפגשים שמתחילים מהמועד הזה (UTC).</summary>
    public DateTime? From { get; set; }

    /// <summary>רק מפגשים שמתחילים עד המועד הזה (UTC).</summary>
    public DateTime? To { get; set; }

    /// <summary>רק מפגשים שנשארו בהם מקומות.</summary>
    public bool OnlyAvailable { get; set; }

    /// <summary>לכלול גם מפגשים שכבר התחילו (ברירת מחדל: רק עתידיים).</summary>
    public bool IncludePast { get; set; }

    public SessionSortBy SortBy { get; set; } = SessionSortBy.StartsAt;

    public bool Descending { get; set; }
}
