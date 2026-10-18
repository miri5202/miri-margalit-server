namespace WorkshopHub.Data.Seed;

/// <summary>
/// פרטי משתמשי הדמו. אלה אינם סודות: הם קיימים רק בסביבת הפיתוח המקומית, נוצרים רק כשהגדרת
/// Database:SeedDemoData דולקת, ומתועדים ב-README כדי שאפשר יהיה להתחבר ולהדגים את המערכת.
/// בבסיס הנתונים נשמר רק הגיבוב של הסיסמה.
/// </summary>
public static class DemoData
{
    public const string AdminEmail = "admin@workshophub.local";
    public const string AdminPassword = "Admin#2026!";

    public const string MemberPassword = "Member#2026!";

    public static readonly (string FullName, string Email)[] Members =
    {
        ("רחל כהן", "rachel@workshophub.local"),
        ("שרה לוי", "sara@workshophub.local"),
        ("חנה פרידמן", "chana@workshophub.local"),
        ("לאה שפירא", "leah@workshophub.local"),
        ("מרים ברגר", "miriam@workshophub.local")
    };
}
