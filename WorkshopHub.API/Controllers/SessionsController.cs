using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using WorkshopHub.API.Security;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.API.Controllers;

/// <summary>
/// מפגשים – המשאב המוגבל. כאן נמצאת הפעולה שמתחרים עליה: הרשמה למפגש
/// (POST /api/sessions/{sessionId}/registrations).
/// </summary>
[Route("api/sessions")]
[Authorize]
public class SessionsController : ApiControllerBase
{
    private readonly ISessionService _sessionService;
    private readonly IRegistrationService _registrationService;

    public SessionsController(ISessionService sessionService, IRegistrationService registrationService)
    {
        _sessionService = sessionService;
        _registrationService = registrationService;
    }

    /// <summary>רשימת מפגשים עם סינון, מיון ו-pagination. פתוח לכולם.</summary>
    /// <remarks>למשתמשת מחוברת, כל מפגש כולל גם את מצב ההרשמה שלה (myRegistrationId / myRegistrationStatus).</remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<SessionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] SessionQuery query, CancellationToken cancellationToken) =>
        Ok(await _sessionService.SearchAsync(query, CurrentUserOrNull, cancellationToken));

    /// <summary>מפגש לפי מזהה. התשובה כוללת את rowVersion, שנדרש בעדכון.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _sessionService.GetByIdAsync(id, CurrentUserOrNull, cancellationToken));

    /// <summary>עדכון מפגש (מנהלת בלבד). חובה לשלוח את ה-rowVersion שהתקבל ב-GET.</summary>
    /// <response code="409">
    /// המפגש השתנה מאז שנטען (concurrency_conflict), או שמספר המקומות החדש קטן ממספר הנרשמות.
    /// </response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] SessionUpdateRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await _sessionService.UpdateAsync(id, request, cancellationToken));

    /// <summary>מחיקת מפגש (מנהלת בלבד). נדחית ב-409 אם יש אליו הרשמות פעילות.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _sessionService.DeleteAsync(id, cancellationToken));

    /// <summary>הרשמה של המשתמשת המחוברת למפגש – הפעולה על המשאב המוגבל.</summary>
    /// <remarks>
    /// גוף הבקשה אופציונלי: { "joinWaitlistIfFull": true } מצרף לרשימת ההמתנה אם המפגש מלא.
    /// </remarks>
    /// <response code="201">ההרשמה נוצרה (status: Confirmed או Waitlisted). ה-header Location מצביע עליה.</response>
    /// <response code="409">
    /// session_full – המפגש מלא; already_registered – כבר רשומה;
    /// concurrency_conflict – משתתפת אחרת תפסה את המקום באותו רגע; session_already_started – המפגש התחיל.
    /// </response>
    [HttpPost("{sessionId:int}/registrations")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateRegistration(
        [FromRoute] int sessionId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RegistrationCreateRequest? request,
        CancellationToken cancellationToken)
    {
        // מי נרשמת נקבע לפי ה-JWT בלבד – הלקוח לא יכול לרשום משתמשת אחרת
        var result = await _registrationService.RegisterAsync(
            sessionId,
            request ?? new RegistrationCreateRequest(),
            CurrentUser,
            cancellationToken);

        if (result.IsFailure)
            return ToErrorResult(result.Error!);

        return CreatedAtAction(
            nameof(RegistrationsController.GetById),
            "Registrations",
            new { id = result.Value.Id },
            result.Value);
    }

    /// <summary>ההרשמות למפגש, עם pagination (מנהלת בלבד).</summary>
    [HttpGet("{sessionId:int}/registrations")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(PagedResult<RegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRegistrations(
        [FromRoute] int sessionId,
        [FromQuery] RegistrationQuery query,
        CancellationToken cancellationToken) =>
        ToActionResult(await _registrationService.GetBySessionAsync(sessionId, query, cancellationToken));
}
