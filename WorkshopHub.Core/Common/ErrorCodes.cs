namespace WorkshopHub.Core.Common;

/// <summary>קודי שגיאה יציבים שחוזרים ללקוח בשדה code. הלקוח מחליט לפיהם מה להציג.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "validation_error";
    public const string Unauthorized = "unauthorized";
    public const string InvalidCredentials = "invalid_credentials";
    public const string Forbidden = "forbidden";
    public const string NotFound = "not_found";
    public const string InternalError = "internal_error";

    public const string EmailTaken = "email_taken";
    public const string NameTaken = "name_taken";
    public const string CategoryInUse = "category_in_use";
    public const string RelatedEntityNotFound = "related_entity_not_found";

    public const string SessionNotInFuture = "session_not_in_future";
    public const string SessionAlreadyStarted = "session_already_started";
    public const string SessionFull = "session_full";
    public const string CapacityBelowSeatsTaken = "capacity_below_seats_taken";
    public const string HasActiveRegistrations = "has_active_registrations";

    public const string AlreadyRegistered = "already_registered";
    public const string AlreadyCancelled = "already_cancelled";

    /// <summary>שתי בקשות התחרו על אותו משאב, והבקשה הזו הפסידה.</summary>
    public const string ConcurrencyConflict = "concurrency_conflict";
}
