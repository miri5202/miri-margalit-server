using WorkshopHub.Core.Entities;

namespace WorkshopHub.Core.Interfaces.Repositories;

public interface ICategoryRepository
{
    /// <summary>כל הקטגוריות, ממוינות לפי שם (קריאה בלבד).</summary>
    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>קטגוריה לפי מזהה (קריאה בלבד).</summary>
    Task<Category?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>קטגוריה לפי מזהה, במעקב של ה-DbContext (לעדכון או למחיקה).</summary>
    Task<Category?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, int? exceptId = null, CancellationToken cancellationToken = default);

    Task<bool> HasWorkshopsAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    void Remove(Category category);
}
