using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Common;

namespace WorkshopHub.API.Extensions;

/// <summary>
/// המקום היחיד שבו נבנית תשובת שגיאה. Controllers, ה-middleware של השגיאות, ולידציית ה-ModelState
/// ואירועי ה-JWT – כולם עוברים דרכו, ולכן כל שגיאה ב-API חוזרת באותו מבנה JSON.
/// </summary>
public static class ErrorResponseFactory
{
    public static ErrorResponse Create(
        HttpContext context,
        int status,
        string code,
        string message,
        IDictionary<string, string[]>? errors = null) =>
        new()
        {
            Status = status,
            Code = code,
            Message = message,
            CorrelationId = context.GetCorrelationId(),
            Errors = errors
        };

    /// <summary>כותב תשובת שגיאה ישירות ל-response (לשימוש מחוץ ל-MVC: middleware ואירועי authentication).</summary>
    public static Task WriteAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Create(context, status, code, message), CancellationToken.None);
    }

    /// <summary>
    /// בדיקת ה-ModelState: כש-Data Annotations על ה-DTO נכשלות, [ApiController] מפעיל את הפונקציה הזו
    /// לפני שהבקשה מגיעה ל-action, והיא מחזירה 400 במבנה השגיאה האחיד עם פירוט לפי שדה.
    /// </summary>
    public static IActionResult FromInvalidModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "הערך אינו תקין." : error.ErrorMessage)
                    .ToArray());

        var body = Create(
            context.HttpContext,
            StatusCodes.Status400BadRequest,
            ErrorCodes.ValidationError,
            "הנתונים שנשלחו אינם תקינים.",
            errors);

        return new BadRequestObjectResult(body);
    }
}
