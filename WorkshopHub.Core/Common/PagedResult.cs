namespace WorkshopHub.Core.Common;

/// <summary>עמוד אחד מתוך רשימה, יחד עם המידע שהלקוח צריך כדי להציג pagination.</summary>
public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>מספר הרשומות הכולל שעונות על הסינון (לא רק בעמוד הזה).</summary>
    public int TotalCount { get; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
