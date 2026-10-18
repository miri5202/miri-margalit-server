using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Data.Extensions;

namespace WorkshopHub.Data.Repositories;

public class RegistrationRepository : IRegistrationRepository
{
    private readonly AppDbContext _context;

    public RegistrationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Registration?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        // נטען יחד עם המפגש (במעקב): ביטול הרשמה משנה גם את מונה המקומות של המפגש
        _context.Registrations
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Registration?> GetDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Registrations
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Session)
                .ThenInclude(s => s.Workshop)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> HasActiveAsync(int sessionId, int userId, CancellationToken cancellationToken = default) =>
        _context.Registrations.AnyAsync(
            r => r.SessionId == sessionId && r.UserId == userId && r.Status != RegistrationStatus.Cancelled,
            cancellationToken);

    public async Task<IReadOnlyList<Registration>> GetNextWaitlistedAsync(int sessionId, int take, CancellationToken cancellationToken = default)
    {
        if (take <= 0)
            return Array.Empty<Registration>();

        return await _context.Registrations
            .Where(r => r.SessionId == sessionId && r.Status == RegistrationStatus.Waitlisted)
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Registration>> GetActiveByUserForSessionsAsync(
        int userId,
        IReadOnlyCollection<int> sessionIds,
        CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0)
            return Array.Empty<Registration>();

        // שאילתה אחת לכל המפגשים שבעמוד (WHERE SessionId IN (...)) – לא שאילתה לכל מפגש
        var idList = sessionIds.ToList();
        return await _context.Registrations
            .AsNoTracking()
            .Where(r => r.UserId == userId
                        && r.Status != RegistrationStatus.Cancelled
                        && idList.Contains(r.SessionId))
            .ToListAsync(cancellationToken);
    }

    public Task<PagedResult<Registration>> GetByUserAsync(int userId, RegistrationQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<Registration> registrations = _context.Registrations
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Session)
                .ThenInclude(s => s.Workshop)
            .Where(r => r.UserId == userId);

        if (query.Status is RegistrationStatus status)
            registrations = registrations.Where(r => r.Status == status);

        return registrations
            .OrderByDescending(r => r.Session.StartsAtUtc)
            .ThenByDescending(r => r.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);
    }

    public Task<PagedResult<Registration>> GetBySessionAsync(int sessionId, RegistrationQuery query, CancellationToken cancellationToken = default)
    {
        IQueryable<Registration> registrations = _context.Registrations
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Session)
                .ThenInclude(s => s.Workshop)
            .Where(r => r.SessionId == sessionId);

        if (query.Status is RegistrationStatus status)
            registrations = registrations.Where(r => r.Status == status);

        return registrations
            .OrderBy(r => r.CreatedAtUtc)
            .ThenBy(r => r.Id)
            .ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);
    }

    public async Task AddAsync(Registration registration, CancellationToken cancellationToken = default) =>
        await _context.Registrations.AddAsync(registration, cancellationToken);
}
