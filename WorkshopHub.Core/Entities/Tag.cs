namespace WorkshopHub.Core.Entities;

/// <summary>תגית. קשר many-to-many: לסדנה יש כמה תגיות, ותגית משויכת לכמה סדנאות.</summary>
public class Tag
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Workshop> Workshops { get; set; } = new List<Workshop>();
}
