namespace WorkshopHub.Data.Querying;

/// <summary>
/// חיפוש טקסט חופשי. ב-PostgreSQL ‏LIKE (ו-Contains של LINQ) רגיש לאותיות גדולות וקטנות, ולכן החיפוש
/// משתמש ב-ILIKE. התווים % ו-_ בטקסט שהמשתמשת הקלידה מקבלים escape, כדי שיחופשו כפשוטם ולא כתבנית.
/// </summary>
internal static class LikePattern
{
    public const string EscapeCharacter = "\\";

    /// <summary>תבנית "מכיל את" – ‎%term%‎ – לשימוש עם EF.Functions.ILike.</summary>
    public static string Containing(string term) =>
        "%" + term.Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
                  .Replace("%", EscapeCharacter + "%")
                  .Replace("_", EscapeCharacter + "_") + "%";
}
