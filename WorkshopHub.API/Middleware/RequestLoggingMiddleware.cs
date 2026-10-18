using System.Diagnostics;

namespace WorkshopHub.API.Middleware;

/// <summary>
/// כותב שורת לוג לכל בקשה נכנסת ולכל תשובה. רץ אחרי <see cref="CorrelationIdMiddleware"/>,
/// ולכן השורות כבר נושאות את ה-CorrelationId.
/// נכתבים רק ה-method, הנתיב, הסטטוס ומשך הזמן – לא גוף הבקשה ולא ה-headers,
/// כדי שסיסמאות, tokens ופרטי הרשמה לעולם לא יגיעו ללוג.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        _logger.LogInformation("HTTP {Method} {Path} started", method, path);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        catch
        {
            // הפרטים של החריגה נכתבים ב-ExceptionHandlingMiddleware; כאן רק מסמנים שהבקשה לא הסתיימה כרגיל
            _logger.LogInformation(
                "HTTP {Method} {Path} ended with an unhandled exception after {ElapsedMs} ms",
                method, path, stopwatch.ElapsedMilliseconds);
            throw;
        }

        _logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
            method, path, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
}
