using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Interfaces.Repositories;

namespace WorkshopHub.Data.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly AppDbContext _context;

    public CategoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Category?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Categories.AnyAsync(c => c.Id == id, cancellationToken);

    public Task<bool> NameExistsAsync(string name, int? exceptId = null, CancellationToken cancellationToken = default) =>
        _context.Categories.AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId), cancellationToken);

    public Task<bool> HasWorkshopsAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Workshops.AnyAsync(w => w.CategoryId == id, cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default) =>
        await _context.Categories.AddAsync(category, cancellationToken);

    public void Remove(Category category) => _context.Categories.Remove(category);
}
