using AutoMapper;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Categories;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(
        ICategoryRepository categories,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        ILogger<CategoryService> logger)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categories.GetAllAsync(cancellationToken);
        return _mapper.Map<List<CategoryResponse>>(categories);
    }

    public async Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetByIdAsync(id, cancellationToken);
        if (category is null)
            return Errors.Categories.NotFound(id);

        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<Result<CategoryResponse>> CreateAsync(CategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        if (await _categories.NameExistsAsync(name, null, cancellationToken))
            return Errors.Categories.NameTaken(name);

        var category = _mapper.Map<Category>(request);
        category.Name = name;

        await _categories.AddAsync(category, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            return Errors.Categories.NameTaken(name);
        }

        _logger.LogInformation("Category {CategoryId} created", category.Id);
        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<Result<CategoryResponse>> UpdateAsync(int id, CategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetForUpdateAsync(id, cancellationToken);
        if (category is null)
            return Errors.Categories.NotFound(id);

        var name = request.Name.Trim();

        if (await _categories.NameExistsAsync(name, id, cancellationToken))
            return Errors.Categories.NameTaken(name);

        _mapper.Map(request, category);
        category.Name = name;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            return Errors.Categories.NameTaken(name);
        }

        _logger.LogInformation("Category {CategoryId} updated", category.Id);
        return _mapper.Map<CategoryResponse>(category);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await _categories.GetForUpdateAsync(id, cancellationToken);
        if (category is null)
            return Errors.Categories.NotFound(id);

        if (await _categories.HasWorkshopsAsync(id, cancellationToken))
            return Errors.Categories.InUse();

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Category {CategoryId} deleted", id);
        return Result.Success();
    }
}
