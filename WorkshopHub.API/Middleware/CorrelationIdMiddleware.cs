namespace WorkshopHub.API.Middleware;

/// <summary>
/// מייצר CorrelationId לכל בקשה ומכניס אותו ל-scope של הלוגים, כך שכל שורת לוג שנכתבת במהלך הבקשה –
/// בכל שכבה – נושאת את אותו מזהה, ואפשר לשחזר בקשה שלמה מתוך לוג משותף.
/// המזהה חוזר ללקוח ב-header X-Correlation-ID וגם בגוף של כל תשובת שגיאה.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    private const int MaxIncomingLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    // שימי לב: middleware נוצר פעם אחת לכל חיי האפליקציה (כמו singleton).
    // לכן מזריקים ל-constructor רק שירותים שהם singleton בעצמם (כמו ILogger).
    // שירות scoped (למשל DbContext) שיוזרק כאן "ייתפס" ויחיה לנצח – זה הבאג של singleton שמחזיק scoped.
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);

        context.Items[ItemKey] = correlationId;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // כל לוג שנכתב בתוך ה-using הזה מקבל את המאפיין CorrelationId (NLog קורא אותו עם ${scopeproperty})
        using (_logger.BeginScope(new Dictionary<string, object> { [ItemKey] = correlationId }))
        {
            await _next(context);
        }
    }

    /// <summary>
    /// אם הלקוח (או שירות אחר בשרשרת) כבר שלח מזהה תקין – ממשיכים איתו, כדי שאפשר יהיה לעקוב אחרי הבקשה
    /// מקצה לקצה. אחרת מייצרים מזהה חדש. ערך מהלקוח נכנס ללוג, ולכן מקבלים רק תווים בטוחים ובאורך מוגבל.
    /// </summary>
    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();

        if (incoming.Length is > 0 and <= MaxIncomingLength
            && incoming.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
        {
            return incoming;
        }

        return Guid.NewGuid().ToString("N");
    }
}
