namespace WorkshopHub.Core.Interfaces.Repositories;

/// <summary>
/// מכין את בסיס הנתונים בעליית השרת: מחיל migrations ומכניס נתוני דמו.
/// הממשק מוגדר ב-Core כדי ש-Program.cs יוכל להפעיל אותו בלי להכיר את ה-DbContext.
/// </summary>
public interface IDbInitializer
{
    Task InitializeAsync(bool applyMigrations, bool seedDemoData, CancellationToken cancellationToken = default);
}
