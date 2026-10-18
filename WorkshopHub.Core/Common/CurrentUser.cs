using WorkshopHub.Core.Enums;

namespace WorkshopHub.Core.Common;

/// <summary>
/// המשתמש שביצע את הבקשה, כפי שזוהה מה-JWT. ה-Controller בונה אותו מה-claims ומעביר לשכבת ה-Service,
/// כך שבדיקות ההרשאה העסקיות (למשל "רק בעלת ההרשמה או מנהלת") נעשות ב-Service ולא ב-Controller.
/// </summary>
public sealed record CurrentUser(int Id, UserRole Role)
{
    public bool IsAdmin => Role == UserRole.Admin;
}
