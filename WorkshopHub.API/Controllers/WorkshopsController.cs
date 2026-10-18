using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.API.Security;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.API.Controllers;

/// <summary>סדנאות. קריאה – פתוחה לכולם; יצירה, עדכון ומחיקה – למנהלות בלבד.</summary>
[Route("api/workshops")]
[Authorize(Roles = Roles.Admin)]
public class WorkshopsController : ApiControllerBase
{
    private readonly IWorkshopService _workshopService;
    private readonly ISessionService _sessionService;

    public WorkshopsController(IWorkshopService workshopService, ISessionService sessionService)
    {
        _workshopService = workshopService;
        _sessionService = sessionService;
    }

    /// <summary>רשימת סדנאות עם סינון (חיפוש, קטגוריה, תגית), מיון ו-pagination.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<WorkshopSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] WorkshopQuery query, CancellationToken cancellationToken) =>
        Ok(await _workshopService.SearchAsync(query, cancellationToken));

    /// <summary>סדנה לפי מזהה, כולל הקטגוריה, התגיות והמפגשים שלה.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WorkshopDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _workshopService.GetByIdAsync(id, cancellationToken));

    /// <summary>יצירת סדנה (מנהלת בלבד).</summary>
    /// <response code="400">הנתונים לא תקינים, או שהקטגוריה / אחת התגיות לא קיימת.</response>
    [HttpPost]
    [ProducesResponseType(typeof(WorkshopDetailsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] WorkshopRequest request, CancellationToken cancellationToken)
    {
        var result = await _workshopService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ToErrorResult(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>עדכון סדנה, כולל רשימת התגיות שלה (מנהלת בלבד).</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(WorkshopDetailsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] int id, [FromBody] WorkshopRequest request, CancellationToken cancellationToken) =>
        ToActionResult(await _workshopService.UpdateAsync(id, request, cancellationToken));

    /// <summary>מחיקת סדנה (מנהלת בלבד). נדחית ב-409 אם יש הרשמות פעילות למפגשים שלה.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _workshopService.DeleteAsync(id, cancellationToken));

    /// <summary>יצירת מפגש חדש לסדנה (מנהלת בלבד).</summary>
    /// <response code="201">המפגש נוצר. ה-header Location מצביע על /api/sessions/{id}.</response>
    [HttpPost("{workshopId:int}/sessions")]
    [ProducesResponseType(typeof(SessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateSession(
        [FromRoute] int workshopId,
        [FromBody] SessionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sessionService.CreateAsync(workshopId, request, cancellationToken);
        if (result.IsFailure)
            return ToErrorResult(result.Error!);

        return CreatedAtAction(
            nameof(SessionsController.GetById),
            "Sessions",
            new { id = result.Value.Id },
            result.Value);
    }
}
