using WorkshopHub.API.Extensions;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.Exceptions;

namespace WorkshopHub.API.Middleware;

/// <summary>
/// טיפול גלובלי בשגיאות – ה-middleware הראשון ב-pipeline, ולכן הוא עוטף את כל מה שבא אחריו.
/// כל חריגה שלא טופלה נתפסת כאן, נכתבת ללוג, וחוזרת ללקוח כ-JSON במבנה האחיד – בלי stack trace
/// ובלי פרטים פנימיים. את הפרטים מוצאים בלוג לפי ה-CorrelationId שחוזר בתשובה.
/// (מצבים עסקיים צפויים – 404, 409 וכו' – לא מגיעים לכאן: ה-Service מחזיר אותם כ-Result.)
/// </summary>
public class ExceptionHandlingMiddleware
{
    private const int StatusClientClosedRequest = 499;

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // הלקוח סגר את החיבור, וה-CancellationToken עצר את העבודה באמצע. זה לא כשל של השרת.
            using (BeginCorrelationScope(context))
            {
                _logger.LogInformation(
                    "Request {Method} {Path} was cancelled by the client",
                    context.Request.Method, context.Request.Path.Value);
            }

            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusClientClosedRequest;
        }
        catch (ConcurrencyConflictException exception)
        {
            // רשת ביטחון: ה-Services תופסים את החריגה הזו בעצמם ומחזירים 409 עם הודעה מדויקת.
            // אם פעולה חדשה תשכח לתפוס אותה, הלקוח עדיין יקבל 409 ולא 500.
            using (BeginCorrelationScope(context))
            {
                _logger.LogWarning(
                    exception,
                    "Concurrency conflict reached the global handler for {Method} {Path}",
                    context.Request.Method, context.Request.Path.Value);
            }

            await WriteErrorAsync(
                context,
                StatusCodes.Status409Conflict,
                ErrorCodes.ConcurrencyConflict,
                "המשאב השתנה על ידי בקשה אחרת. רענני ונסי שוב.");
        }
        catch (Exception exception)
        {
            using (BeginCorrelationScope(context))
            {
                _logger.LogError(
                    exception,
                    "Unhandled exception while processing {Method} {Path}",
                    context.Request.Method, context.Request.Path.Value);
            }

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                "אירעה שגיאה בלתי צפויה בשרת. אפשר לנסות שוב; אם הבעיה חוזרת, מסרי לתמיכה את מזהה הבקשה.");
        }
    }

    /// <summary>
    /// ה-scope של ה-CorrelationId נפתח ב-middleware פנימי יותר, והוא כבר נסגר כשהחריגה מגיעה לכאן.
    /// פותחים אותו מחדש כדי שגם שורת השגיאה תישא את מזהה הבקשה.
    /// </summary>
    private IDisposable? BeginCorrelationScope(HttpContext context) =>
        _logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdMiddleware.ItemKey] = context.GetCorrelationId()
        });

    private static Task WriteErrorAsync(HttpContext context, int status, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            // התשובה כבר התחילה להישלח – אי אפשר לשנות סטטוס או גוף
            return Task.CompletedTask;
        }

        context.Response.Clear();
        return ErrorResponseFactory.WriteAsync(context, status, code, message);
    }
}
