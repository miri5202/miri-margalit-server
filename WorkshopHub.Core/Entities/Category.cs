namespace WorkshopHub.Core.Entities;

/// <summary>קטגוריה של סדנאות. קשר one-to-many: לקטגוריה יש הרבה סדנאות.</summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Workshop> Workshops { get; set; } = new List<Workshop>();
}
