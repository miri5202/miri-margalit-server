using AutoMapper;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Tags;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _tags;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<TagService> _logger;

    public TagService(ITagRepository tags, IUnitOfWork unitOfWork, IMapper mapper, ILogger<TagService> logger)
    {
        _tags = tags;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tags = await _tags.GetAllAsync(cancellationToken);
        return _mapper.Map<List<TagResponse>>(tags);
    }

    public async Task<Result<TagResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tags.GetByIdAsync(id, cancellationToken);
        if (tag is null)
            return Errors.Tags.NotFound(id);

        return _mapper.Map<TagResponse>(tag);
    }

    public async Task<Result<TagResponse>> CreateAsync(TagRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _tags.NameExistsAsync(name, cancellationToken))
            return Errors.Tags.NameTaken(name);

        var tag = _mapper.Map<Tag>(request);
        tag.Name = name;

        await _tags.AddAsync(tag, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            return Errors.Tags.NameTaken(name);
        }

        _logger.LogInformation("Tag {TagId} created", tag.Id);
        return _mapper.Map<TagResponse>(tag);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await _tags.GetForUpdateAsync(id, cancellationToken);
        if (tag is null)
            return Errors.Tags.NotFound(id);

        // מחיקת תגית מסירה אותה מכל הסדנאות (cascade על טבלת הקישור WorkshopTags), הסדנאות עצמן לא נפגעות
        _tags.Remove(tag);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tag {TagId} deleted", id);
        return Result.Success();
    }
}
