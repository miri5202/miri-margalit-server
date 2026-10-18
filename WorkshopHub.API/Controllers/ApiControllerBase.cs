using Microsoft.AspNetCore.Mvc;
using WorkshopHub.API.Extensions;
using WorkshopHub.Core.Common;

namespace WorkshopHub.API.Controllers;

/// <summary>
/// בסיס לכל ה-Controllers. ה-Controllers עצמם רזים: הם קושרים את הבקשה (route / query / body),
/// קוראים ל-Service אחד, ומתרגמים את ה-Result שחזר לסטטוס HTTP. אין בהם לוגיקה עסקית ואין גישה לנתונים.
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>המשתמש המחובר, או null אם הבקשה אנונימית (ב-endpoints ציבוריים).</summary>
    protected CurrentUser? CurrentUserOrNull => User.ToCurrentUser();

    /// <summary>המשתמש המחובר. לשימוש רק ב-actions שמוגנים ב-[Authorize].</summary>
    protected CurrentUser CurrentUser =>
        User.ToCurrentUser()
        ?? throw new InvalidOperationException("The action requires an authenticated user, but the token carries no valid user claims.");

    /// <summary>הצלחה ללא תוכן -> 204; כישלון -> סטטוס לפי סוג השגיאה.</summary>
    protected IActionResult ToActionResult(Result result) =>
        result.IsSuccess ? NoContent() : ToErrorResult(result.Error!);

    /// <summary>הצלחה עם ערך -> 200; כישלון -> סטטוס לפי סוג השגיאה.</summary>
    protected IActionResult ToActionResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ToErrorResult(result.Error!);

    /// <summary>התרגום היחיד במערכת מסוג שגיאה עסקית לסטטוס HTTP.</summary>
    protected IActionResult ToErrorResult(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        return StatusCode(status, ErrorResponseFactory.Create(HttpContext, status, error.Code, error.Message));
    }
}
