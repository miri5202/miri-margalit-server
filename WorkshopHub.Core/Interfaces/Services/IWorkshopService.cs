using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Workshops;

namespace WorkshopHub.Core.Interfaces.Services;

public interface IWorkshopService
{
    Task<PagedResult<WorkshopSummaryResponse>> SearchAsync(WorkshopQuery query, CancellationToken cancellationToken = default);

    Task<Result<WorkshopDetailsResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<WorkshopDetailsResponse>> CreateAsync(WorkshopRequest request, CancellationToken cancellationToken = default);

    Task<Result<WorkshopDetailsResponse>> UpdateAsync(int id, WorkshopRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
