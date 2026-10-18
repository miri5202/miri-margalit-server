using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WorkshopHub.Data.Converters;

/// <summary>
/// כל התאריכים במערכת הם UTC. SQL Server לא שומר את ה-Kind של התאריך, ולכן בקריאה מסמנים אותו במפורש
/// כ-UTC – כך ה-JSON יוצא עם הסיומת Z, והלקוח ממיר נכון לשעון המקומי.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
