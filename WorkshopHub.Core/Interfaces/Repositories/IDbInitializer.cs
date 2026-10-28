using WorkshopHub.Core.Settings;

namespace WorkshopHub.Core.Interfaces.Repositories;

/// <summary>
/// מכין את בסיס הנתונים בעליית השרת: מחיל migrations, מכניס נתוני דמו (רק בפיתוח),
/// מוודא את חשבון המנהלת מתוך ההגדרות, ונועל את חשבונות הדמו כשנתוני הדמו כבויים.
/// הממשק מוגדר ב-Core כדי ש-Program.cs יוכל להפעיל אותו בלי להכיר את ה-DbContext.
/// </summary>
public interface IDbInitializer
{
    Task InitializeAsync(DatabaseSettings database, AdminAccountSettings adminAccount, CancellationToken cancellationToken = default);
}
