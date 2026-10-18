namespace WorkshopHub.Tests.Helpers;

/// <summary>שעון קבוע לבדיקות. ה-Services מקבלים TimeProvider בהזרקה, ולכן אפשר "לעצור את הזמן".</summary>
internal sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    public FixedTimeProvider(DateTime utcNow)
    {
        _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
