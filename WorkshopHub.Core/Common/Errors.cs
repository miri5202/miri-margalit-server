namespace WorkshopHub.Core.Common;

/// <summary>
/// קטלוג השגיאות העסקיות של המערכת – מקום אחד שבו מוגדרים הסוג, הקוד וההודעה של כל שגיאה.
/// </summary>
public static class Errors
{
    public static class General
    {
        public static Error Forbidden() =>
            new(ErrorType.Forbidden, ErrorCodes.Forbidden, "אין לך הרשאה לבצע את הפעולה הזו.");
    }

    public static class Auth
    {
        // אותה הודעה גם כשהמייל לא קיים וגם כשהסיסמה שגויה – כדי לא לחשוף אילו כתובות רשומות במערכת
        public static Error InvalidCredentials() =>
            new(ErrorType.Unauthorized, ErrorCodes.InvalidCredentials, "כתובת המייל או הסיסמה שגויים.");

        public static Error EmailTaken() =>
            new(ErrorType.Conflict, ErrorCodes.EmailTaken, "כבר קיים חשבון עם כתובת המייל הזו.");
    }

    public static class Users
    {
        public static Error NotFound(int id) =>
            new(ErrorType.NotFound, ErrorCodes.NotFound, $"משתמש עם מזהה {id} לא נמצא.");
    }

    public static class Categories
    {
        public static Error NotFound(int id) =>
            new(ErrorType.NotFound, ErrorCodes.NotFound, $"קטגוריה עם מזהה {id} לא נמצאה.");

        public static Error NameTaken(string name) =>
            new(ErrorType.Conflict, ErrorCodes.NameTaken, $"כבר קיימת קטגוריה בשם \"{name}\".");

        public static Error InUse() =>
            new(ErrorType.Conflict, ErrorCodes.CategoryInUse, "אי אפשר למחוק קטגוריה שיש בה סדנאות.");
    }

    public static class Tags
    {
        public static Error NotFound(int id) =>
            new(ErrorType.NotFound, ErrorCodes.NotFound, $"תגית עם מזהה {id} לא נמצאה.");

        public static Error NameTaken(string name) =>
            new(ErrorType.Conflict, ErrorCodes.NameTaken, $"כבר קיימת תגית בשם \"{name}\".");
    }

    public static class Workshops
    {
        public static Error NotFound(int id) =>
            new(ErrorType.NotFound, ErrorCodes.NotFound, $"סדנה עם מזהה {id} לא נמצאה.");

        public static Error CategoryNotFound(int categoryId) =>
            new(ErrorType.Validation, ErrorCodes.RelatedEntityNotFound, $"קטגוריה עם מזהה {categoryId} לא קיימת.");

        public static Error TagsNotFound(IEnumerable<int> missingIds) =>
            new(ErrorType.Validation, ErrorCodes.RelatedEntityNotFound,
                $"התגיות הבאות לא קיימות: {string.Join(", ", missingIds)}.");

        public static Error HasActiveRegistrations() =>
            new(ErrorType.Conflict, ErrorCodes.HasActiveRegistrations,
                "אי אפשר למחוק סדנה שיש הרשמות פעילות למפגשים שלה.");
    }

    public static class Sessions
    {
        public static Error NotFound(int id) =>
            new(ErrorType.NotFound, ErrorCodes.NotFound, $"מפגש עם מזהה {id} לא נמצא.");

        public static Error MustBeInFuture() =>
            new(ErrorType.Validation, ErrorCodes.SessionNotInFuture, "מועד המפגש חייב להיות בעתיד.");

        public static Error AlreadyStarted() =>
            new(ErrorType.Conflict, ErrorCodes.SessionAlreadyStarted, "המפגש כבר התחיל, ואי אפשר לשנות את ההרשמות אליו.");

        public static Error Full() =>
            new(ErrorType.Conflict, ErrorCodes.SessionFull, "המפגש מלא. אפשר להצטרף לרשימת ההמתנה.");

        /// <summary>ההתנגשות שבלב הפרויקט: מישהי אחרת שינתה את המפגש בין הקריאה לשמירה.</summary>
        public static Error SeatTakenConcurrently() =>
            new(ErrorType.Conflict, ErrorCodes.ConcurrencyConflict,
                "המקום נתפס על ידי משתתפת אחרת ממש ברגע זה. רענני את הרשימה ונסי שוב.");

        public static Error ModifiedConcurrently() =>
            new(ErrorType.Conflict, ErrorCodes.ConcurrencyConflict,
                "המפגש השתנה מאז שטענת אותו (למשל, מישהי נרשמה בינתיים). טעני אותו מחדש ונסי שוב.");

        public static Error CapacityBelowSeatsTaken(int seatsTaken) =>
            new(ErrorType.Conflict, ErrorCodes.CapacityBelowSeatsTaken,
                $"אי אפשר להקטין את מספר המקומות מתחת למספר הנרשמות המאושרות ({seatsTaken}).");

        public static Error HasActiveRegistrations() =>
            new(ErrorType.Conflict, ErrorCodes.HasActiveRegistrations, "אי אפשר למחוק מפגש שיש אליו הרשמות פעילות.");
    }

    public static class Registrations
    {
        public static Error NotFound(int id) =>
            new(ErrorType.NotFound, ErrorCodes.NotFound, $"הרשמה עם מזהה {id} לא נמצאה.");

        public static Error AlreadyRegistered() =>
            new(ErrorType.Conflict, ErrorCodes.AlreadyRegistered, "את כבר רשומה למפגש הזה (או ממתינה לו).");

        public static Error AlreadyCancelled() =>
            new(ErrorType.Conflict, ErrorCodes.AlreadyCancelled, "ההרשמה הזו כבר בוטלה.");

        public static Error ChangedConcurrently() =>
            new(ErrorType.Conflict, ErrorCodes.ConcurrencyConflict,
                "מצב המפגש השתנה בזמן הביטול. רענני את הרשימה ונסי שוב.");
    }
}
