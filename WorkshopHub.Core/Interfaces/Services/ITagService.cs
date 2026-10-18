using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Tags;

namespace WorkshopHub.Core.Interfaces.Services;

public interface ITagService
{
    Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<TagResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<TagResponse>> CreateAsync(TagRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
