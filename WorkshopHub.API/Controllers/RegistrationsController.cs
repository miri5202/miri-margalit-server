using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.API.Controllers;

/// <summary>הרשמות. יצירת הרשמה נעשית דרך המפגש: POST /api/sessions/{sessionId}/registrations.</summary>
[Route("api/registrations")]
[Authorize]
public class RegistrationsController : ApiControllerBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationsController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>הרשמה לפי מזהה. רק בעלת ההרשמה או מנהלת.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _registrationService.GetByIdAsync(id, CurrentUser, cancellationToken));

    /// <summary>ביטול הרשמה. רק בעלת ההרשמה או מנהלת. אם התפנה מקום, הממתינה הבאה בתור מקודמת אליו.</summary>
    /// <response code="204">ההרשמה בוטלה.</response>
    /// <response code="403">ההרשמה שייכת למשתמשת אחרת.</response>
    /// <response code="409">ההרשמה כבר בוטלה, המפגש כבר התחיל, או שמצב המפגש השתנה בזמן הביטול.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _registrationService.CancelAsync(id, CurrentUser, cancellationToken));
}
