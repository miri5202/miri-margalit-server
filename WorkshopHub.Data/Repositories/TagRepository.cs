using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Interfaces.Repositories;

namespace WorkshopHub.Data.Repositories;

public class TagRepository : ITagRepository
{
    private readonly AppDbContext _context;

    public TagRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Tags
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

    public Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Tags
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Tag?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Tag>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return Array.Empty<Tag>();

        // שאילתה אחת עם IN (...) לכל התגיות, במקום שאילתה לכל מזהה
        var idList = ids.ToList();
        return await _context.Tags
            .Where(t => idList.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default) =>
        _context.Tags.AnyAsync(t => t.Name == name, cancellationToken);

    public async Task AddAsync(Tag tag, CancellationToken cancellationToken = default) =>
        await _context.Tags.AddAsync(tag, cancellationToken);

    public void Remove(Tag tag) => _context.Tags.Remove(tag);
}
