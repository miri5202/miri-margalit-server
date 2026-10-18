using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.API.Controllers;

/// <summary>
/// התחברות. לפי מוסכמות REST אין שמות פעולות ב-URL (לא /login), ולכן התחברות היא יצירה של משאב – token:
/// POST /api/tokens.
/// </summary>
[Route("api/tokens")]
public class TokensController : ApiControllerBase
{
    private readonly IAuthService _authService;

    public TokensController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>התחברות – בדיקת מייל וסיסמה והנפקת JWT.</summary>
    /// <response code="200">ההתחברות הצליחה. ה-token נשלח בבקשות הבאות ב-header: Authorization: Bearer {token}</response>
    /// <response code="401">כתובת המייל או הסיסמה שגויים.</response>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] LoginRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await _authService.LoginAsync(request, cancellationToken));
}
