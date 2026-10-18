using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Categories;

namespace WorkshopHub.Core.Interfaces.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<CategoryResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<CategoryResponse>> CreateAsync(CategoryRequest request, CancellationToken cancellationToken = default);

    Task<Result<CategoryResponse>> UpdateAsync(int id, CategoryRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
