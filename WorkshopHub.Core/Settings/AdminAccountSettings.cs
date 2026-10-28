namespace WorkshopHub.Core.Settings;

/// <summary>
/// חשבון המנהלת של האתר המפורסם. הפרטים מגיעים ממשתני סביבה (AdminAccount__Email, AdminAccount__Password)
/// ולא נשמרים ב-repository. בכל עליית שרת מוודאים שהחשבון קיים, שהוא Admin, ושהסיסמה שלו היא זו שהוגדרה.
/// </summary>
public sealed class AdminAccountSettings
{
    public const string SectionName = "AdminAccount";

    public const int MinPasswordLength = 12;

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string FullName { get; set; } = "מנהלת המערכת";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);
}
