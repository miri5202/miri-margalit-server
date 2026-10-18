using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkshopHub.API.Security;
using WorkshopHub.Core.DTOs.Common;
using WorkshopHub.Core.DTOs.Tags;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.API.Controllers;

/// <summary>תגיות של סדנאות (קשר many-to-many). קריאה – פתוחה לכולם; שינוי – למנהלות בלבד.</summary>
[Route("api/tags")]
[Authorize(Roles = Roles.Admin)]
public class TagsController : ApiControllerBase
{
    private readonly ITagService _tagService;

    public TagsController(ITagService tagService)
    {
        _tagService = tagService;
    }

    /// <summary>כל התגיות.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<TagResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        Ok(await _tagService.GetAllAsync(cancellationToken));

    /// <summary>תגית לפי מזהה.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _tagService.GetByIdAsync(id, cancellationToken));

    /// <summary>יצירת תגית (מנהלת בלבד).</summary>
    /// <response code="409">כבר קיימת תגית בשם הזה.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TagResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] TagRequest request, CancellationToken cancellationToken)
    {
        var result = await _tagService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
            return ToErrorResult(result.Error!);

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>מחיקת תגית (מנהלת בלבד). התגית מוסרת מכל הסדנאות שהיא משויכת אליהן.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken cancellationToken) =>
        ToActionResult(await _tagService.DeleteAsync(id, cancellationToken));
}
