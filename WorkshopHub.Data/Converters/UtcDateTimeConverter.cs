using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WorkshopHub.Data.Converters;

/// <summary>
/// כל התאריכים במערכת הם UTC. PostgreSQL (דרך Npgsql) שומר DateTime בעמודת timestamptz ומקבל רק תאריך
/// שה-Kind שלו הוא Utc – תאריך מקומי או לא מוגדר זורק חריגה בשמירה. לכן בכתיבה ממירים כל תאריך ל-UTC,
/// ובקריאה מסמנים אותו במפורש כ-UTC – כך ה-JSON יוצא עם הסיומת Z, והלקוח ממיר נכון לשעון המקומי.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
    {
    }
}
