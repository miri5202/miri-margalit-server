using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Data.Extensions;

namespace WorkshopHub.Data.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly AppDbContext _context;

    public SessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PagedResult<Session>> SearchAsync(SessionQuery query, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Include + ThenInclude: מפגש -> סדנה -> קטגוריה, הכול ב-JOIN אחד
        IQueryable<Session> sessions = _context.Sessions
            .AsNoTracking()
            .Include(s => s.Workshop)
                .ThenInclude(w => w.Category);

        if (!query.IncludePast)
            sessions = sessions.Where(s => s.StartsAtUtc > nowUtc);

        if (query.WorkshopId is int workshopId)
            sessions = sessions.Where(s => s.WorkshopId == workshopId);

        if (query.CategoryId is int categoryId)
            sessions = sessions.Where(s => s.Workshop.CategoryId == categoryId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            sessions = sessions.Where(s => s.Workshop.Title.Contains(term) || s.Workshop.InstructorName.Contains(term));
        }

        if (query.From is DateTime fromUtc)
            sessions = sessions.Where(s => s.StartsAtUtc >= fromUtc);

        if (query.To is DateTime toUtc)
            sessions = sessions.Where(s => s.StartsAtUtc <= toUtc);

        if (query.OnlyAvailable)
            sessions = sessions.Where(s => s.SeatsTaken < s.Capacity);

        sessions = (query.SortBy, query.Descending) switch
        {
            (SessionSortBy.SeatsLeft, false) => sessions.OrderBy(s => s.Capacity - s.SeatsTaken).ThenBy(s => s.StartsAtUtc).ThenBy(s => s.Id),
            (SessionSortBy.SeatsLeft, true) => sessions.OrderByDescending(s => s.Capacity - s.SeatsTaken).ThenBy(s => s.StartsAtUtc).ThenBy(s => s.Id),
            (SessionSortBy.WorkshopTitle, false) => sessions.OrderBy(s => s.Workshop.Title).ThenBy(s => s.StartsAtUtc).ThenBy(s => s.Id),
            (SessionSortBy.WorkshopTitle, true) => sessions.OrderByDescending(s => s.Workshop.Title).ThenBy(s => s.StartsAtUtc).ThenBy(s => s.Id),
            (_, true) => sessions.OrderByDescending(s => s.StartsAtUtc).ThenBy(s => s.Id),
            _ => sessions.OrderBy(s => s.StartsAtUtc).ThenBy(s => s.Id)
        };

        return sessions.ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);
    }

    public Task<Session?> GetDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Sessions
            .AsNoTracking()
            .Include(s => s.Workshop)
                .ThenInclude(w => w.Category)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<Session?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        // בלי AsNoTracking: ה-DbContext עוקב אחרי המפגש וזוכר את ה-RowVersion שנקרא.
        // בזמן השמירה הוא ייכנס לתנאי ה-WHERE של ה-UPDATE.
        _context.Sessions
            .Include(s => s.Workshop)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void SetOriginalRowVersion(Session session, byte[] rowVersion) =>
        _context.Entry(session).Property(s => s.RowVersion).OriginalValue = rowVersion;

    public async Task AddAsync(Session session, CancellationToken cancellationToken = default) =>
        await _context.Sessions.AddAsync(session, cancellationToken);

    public void Remove(Session session) => _context.Sessions.Remove(session);
}
