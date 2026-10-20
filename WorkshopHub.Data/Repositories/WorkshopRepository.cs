using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Workshops;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Data.Extensions;
using WorkshopHub.Data.Querying;

namespace WorkshopHub.Data.Repositories;

public class WorkshopRepository : IWorkshopRepository
{
    private readonly AppDbContext _context;

    public WorkshopRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PagedResult<Workshop>> SearchAsync(WorkshopQuery query, CancellationToken cancellationToken = default)
    {
        // Include טוען את הקטגוריה והתגיות יחד עם הסדנאות – בלי שאילתה נפרדת לכל סדנה (N+1)
        IQueryable<Workshop> workshops = _context.Workshops
            .AsNoTracking()
            .Include(w => w.Category)
            .Include(w => w.Tags);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Containing(query.Search.Trim());
            workshops = workshops.Where(w =>
                EF.Functions.ILike(w.Title, pattern, LikePattern.EscapeCharacter) ||
                EF.Functions.ILike(w.InstructorName, pattern, LikePattern.EscapeCharacter));
        }

        if (query.CategoryId is int categoryId)
            workshops = workshops.Where(w => w.CategoryId == categoryId);

        if (query.TagId is int tagId)
            workshops = workshops.Where(w => w.Tags.Any(t => t.Id == tagId));

        // ThenBy(Id) מבטיח סדר יציב גם כשיש ערכים זהים – אחרת רשומה יכולה להופיע בשני עמודים
        workshops = (query.SortBy, query.Descending) switch
        {
            (WorkshopSortBy.InstructorName, false) => workshops.OrderBy(w => w.InstructorName).ThenBy(w => w.Id),
            (WorkshopSortBy.InstructorName, true) => workshops.OrderByDescending(w => w.InstructorName).ThenBy(w => w.Id),
            (WorkshopSortBy.Category, false) => workshops.OrderBy(w => w.Category.Name).ThenBy(w => w.Title).ThenBy(w => w.Id),
            (WorkshopSortBy.Category, true) => workshops.OrderByDescending(w => w.Category.Name).ThenBy(w => w.Title).ThenBy(w => w.Id),
            (_, true) => workshops.OrderByDescending(w => w.Title).ThenBy(w => w.Id),
            _ => workshops.OrderBy(w => w.Title).ThenBy(w => w.Id)
        };

        return workshops.ToPagedResultAsync(query.Page, query.PageSize, cancellationToken);
    }

    public Task<Workshop?> GetDetailsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Workshops
            .AsNoTracking()
            .Include(w => w.Category)
            .Include(w => w.Tags)
            .Include(w => w.Sessions.OrderBy(s => s.StartsAtUtc))
            // שני אוספים (תגיות ומפגשים) ב-JOIN אחד היו מכפילים שורות; AsSplitQuery מפצל לשאילתות נפרדות
            .AsSplitQuery()
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<Workshop?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Workshops
            .Include(w => w.Tags)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Workshops.AnyAsync(w => w.Id == id, cancellationToken);

    public Task<bool> HasActiveRegistrationsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Registrations.AnyAsync(
            r => r.Session.WorkshopId == id && r.Status != RegistrationStatus.Cancelled,
            cancellationToken);

    public async Task AddAsync(Workshop workshop, CancellationToken cancellationToken = default) =>
        await _context.Workshops.AddAsync(workshop, cancellationToken);

    public void Remove(Workshop workshop) => _context.Workshops.Remove(workshop);
}
