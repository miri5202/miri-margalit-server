using WorkshopHub.Core.Entities;

namespace WorkshopHub.Core.Interfaces.Repositories;

public interface ITagRepository
{
    /// <summary>כל התגיות, ממוינות לפי שם (קריאה בלבד).</summary>
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>תגית לפי מזהה (קריאה בלבד).</summary>
    Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>תגית לפי מזהה, במעקב של ה-DbContext (למחיקה).</summary>
    Task<Tag?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>תגיות לפי רשימת מזהים, בשאילתה אחת ובמעקב של ה-DbContext (לשיוך לסדנה).</summary>
    Task<IReadOnlyList<Tag>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);

    Task<bool> NameExistsAsync(string name, CancellationToken cancellationToken = default);

    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);

    void Remove(Tag tag);
}
