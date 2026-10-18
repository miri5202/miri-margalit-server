using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.API.Controllers;

/// <summary>משתמשים: הרשמה למערכת, פרטי המשתמש וההרשמות שלו למפגשים.</summary>
[Route("api/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IRegistrationService _registrationService;

    public UsersController(IAuthService authService, IUserService userService, IRegistrationService registrationService)
    {
        _authService = authService;
        _userService = userService;
        _registrationService = registrationService;
    }

    /// <summary>הרשמה למערכת – יצירת משתמש חדש (תמיד בתפקיד Member).</summary>
    /// <response code="201">המשתמש נוצר. ה-header Location מצביע עליו.</response>
    /// <response code="400">הנתונים לא תקינים.</response>
    /// <response code="409">כבר קיים חשבון עם כתובת המייל הזו.</response>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        if (result.IsFailure)
            return ToErrorResult(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>פרטי המשתמש המחובר.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        var currentUser = CurrentUser;
        return ToActionResult(await _userService.GetByIdAsync(currentUser.Id, currentUser, cancellationToken));
    }

    /// <summary>פרטי משתמש לפי מזהה. משתמשת רגילה יכולה לראות רק את עצמה; מנהלת – כל משתמש.</summary>
    /// <response code="403">ניסיון לצפות במשתמש אחר ללא הרשאת מנהלת.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _userService.GetByIdAsync(id, CurrentUser, cancellationToken));

    /// <summary>ההרשמות של המשתמש המחובר, עם pagination.</summary>
    [HttpGet("me/registrations")]
    [ProducesResponseType(typeof(PagedResult<RegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyRegistrations([FromQuery] RegistrationQuery query, CancellationToken cancellationToken) =>
        Ok(await _registrationService.GetMineAsync(CurrentUser, query, cancellationToken));
}
