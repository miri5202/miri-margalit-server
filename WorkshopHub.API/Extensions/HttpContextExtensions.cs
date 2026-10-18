using System.Security.Claims;
using WorkshopHub.API.Middleware;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.Enums;

namespace WorkshopHub.API.Extensions;

public static class HttpContextExtensions
{
    /// <summary>ה-CorrelationId של הבקשה הנוכחית (נוצר ב-<see cref="CorrelationIdMiddleware"/>).</summary>
    public static string GetCorrelationId(this HttpContext context) =>
        context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    /// <summary>
    /// בונה את המשתמש הנוכחי מה-claims של ה-JWT (שכבר אומת על ידי ה-authentication middleware).
    /// מחזיר null אם הבקשה אנונימית.
    /// </summary>
    public static CurrentUser? ToCurrentUser(this ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return null;

        var idValue = principal.FindFirst(AppClaimTypes.UserId)?.Value;
        var roleValue = principal.FindFirst(AppClaimTypes.Role)?.Value;

        if (!int.TryParse(idValue, out var id) || !Enum.TryParse<UserRole>(roleValue, out var role))
            return null;

        return new CurrentUser(id, role);
    }
}
