namespace WorkshopHub.Core.Settings;

/// <summary>הגדרות אתחול בסיס הנתונים, נטענות מהקונפיגורציה (סעיף "Database").</summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    /// <summary>להחיל migrations שטרם הוחלו בעליית השרת (יוצר את בסיס הנתונים אם אינו קיים).</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    /// <summary>להכניס נתוני דמו (משתמשים, סדנאות, מפגשים) אם בסיס הנתונים ריק.</summary>
    public bool SeedDemoData { get; set; }
}
