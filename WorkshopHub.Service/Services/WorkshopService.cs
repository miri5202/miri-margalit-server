using AutoMapper;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

public class WorkshopService : IWorkshopService
{
    private readonly IWorkshopRepository _workshops;
    private readonly ICategoryRepository _categories;
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<WorkshopService> _logger;

    public WorkshopService(
        IWorkshopRepository workshops,
        ICategoryRepository categories,
        ITagRepository tags,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<WorkshopService> logger)
    {
        _workshops = workshops;
        _categories = categories;
        _tags = tags;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<WorkshopSummaryResponse>> SearchAsync(WorkshopQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _workshops.SearchAsync(query, cancellationToken);

        return new PagedResult<WorkshopSummaryResponse>(
            _mapper.Map<List<WorkshopSummaryResponse>>(page.Items),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<Result<WorkshopDetailsResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var workshop = await _workshops.GetDetailsAsync(id, cancellationToken);
        if (workshop is null)
            return Errors.Workshops.NotFound(id);

        return _mapper.Map<WorkshopDetailsResponse>(workshop);
    }

    public async Task<Result<WorkshopDetailsResponse>> CreateAsync(WorkshopRequest request, CancellationToken cancellationToken = default)
    {
        var tagsResult = await ResolveRelatedEntitiesAsync(request, cancellationToken);
        if (tagsResult.IsFailure)
            return tagsResult.Error!;

        var workshop = _mapper.Map<Workshop>(request);
        Normalize(workshop);

        foreach (var tag in tagsResult.Value)
            workshop.Tags.Add(tag);

        await _workshops.AddAsync(workshop, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workshop {WorkshopId} created with {TagCount} tags", workshop.Id, workshop.Tags.Count);

        return await LoadResponseAsync(workshop.Id, cancellationToken);
    }

    public async Task<Result<WorkshopDetailsResponse>> UpdateAsync(int id, WorkshopRequest request, CancellationToken cancellationToken = default)
    {
        var workshop = await _workshops.GetForUpdateAsync(id, cancellationToken);
        if (workshop is null)
            return Errors.Workshops.NotFound(id);

        var tagsResult = await ResolveRelatedEntitiesAsync(request, cancellationToken);
        if (tagsResult.IsFailure)
            return tagsResult.Error!;

        _mapper.Map(request, workshop);
        Normalize(workshop);
        SyncTags(workshop, tagsResult.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workshop {WorkshopId} updated", workshop.Id);

        return await LoadResponseAsync(workshop.Id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var workshop = await _workshops.GetForUpdateAsync(id, cancellationToken);
        if (workshop is null)
            return Errors.Workshops.NotFound(id);

        // לא מוחקים סדנה שמשתתפות רשומות למפגשים שלה
        if (await _workshops.HasActiveRegistrationsAsync(id, cancellationToken))
            return Errors.Workshops.HasActiveRegistrations();

        _workshops.Remove(workshop);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Workshop {WorkshopId} deleted", id);
        return Result.Success();
    }

    /// <summary>ולידציה עסקית: הקטגוריה וכל התגיות שנשלחו חייבות להתקיים. מחזיר את התגיות עצמן.</summary>
    private async Task<Result<IReadOnlyList<Tag>>> ResolveRelatedEntitiesAsync(WorkshopRequest request, CancellationToken cancellationToken)
    {
        if (!await _categories.ExistsAsync(request.CategoryId, cancellationToken))
            return Result.Failure<IReadOnlyList<Tag>>(Errors.Workshops.CategoryNotFound(request.CategoryId));

        var tagIds = request.TagIds.Distinct().ToList();

        // שאילתה אחת לכל התגיות
        var tags = await _tags.GetByIdsAsync(tagIds, cancellationToken);

        var missingIds = tagIds.Except(tags.Select(t => t.Id)).ToList();
        if (missingIds.Count > 0)
            return Result.Failure<IReadOnlyList<Tag>>(Errors.Workshops.TagsNotFound(missingIds));

        return Result.Success(tags);
    }

    /// <summary>מעדכן את קשר ה-many-to-many: מסיר תגיות שירדו ומוסיף תגיות חדשות, בלי לגעת באלה שנשארו.</summary>
    private static void SyncTags(Workshop workshop, IReadOnlyList<Tag> requestedTags)
    {
        var requestedIds = requestedTags.Select(t => t.Id).ToHashSet();
        var currentIds = workshop.Tags.Select(t => t.Id).ToHashSet();

        foreach (var removed in workshop.Tags.Where(t => !requestedIds.Contains(t.Id)).ToList())
            workshop.Tags.Remove(removed);

        foreach (var added in requestedTags.Where(t => !currentIds.Contains(t.Id)))
            workshop.Tags.Add(added);
    }

    private static void Normalize(Workshop workshop)
    {
        workshop.Title = workshop.Title.Trim();
        workshop.Description = (workshop.Description ?? string.Empty).Trim();
        workshop.InstructorName = workshop.InstructorName.Trim();
    }

    private async Task<Result<WorkshopDetailsResponse>> LoadResponseAsync(int id, CancellationToken cancellationToken)
    {
        // קריאה מחדש (ללא מעקב) כדי להחזיר תשובה מלאה עם הקטגוריה, התגיות והמפגשים
        var saved = await _workshops.GetDetailsAsync(id, cancellationToken);
        if (saved is null)
            return Errors.Workshops.NotFound(id);

        return _mapper.Map<WorkshopDetailsResponse>(saved);
    }
}
