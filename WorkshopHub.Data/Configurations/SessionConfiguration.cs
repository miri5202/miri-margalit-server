using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("Sessions", table =>
        {
            // קו ההגנה האחרון: גם אם יהיה באג בקוד, בסיס הנתונים עצמו לא יסכים לשמור
            // יותר נרשמות ממקומות.
            table.HasCheckConstraint("CK_Sessions_Capacity_Positive", "[Capacity] > 0");
            table.HasCheckConstraint("CK_Sessions_SeatsTaken_Range", "[SeatsTaken] >= 0 AND [SeatsTaken] <= [Capacity]");
            table.HasCheckConstraint("CK_Sessions_WaitlistCount_NonNegative", "[WaitlistCount] >= 0");
        });

        builder.Property(s => s.Location).IsRequired().HasMaxLength(120);

        // ה-concurrency token: עמודת rowversion ש-SQL Server מעדכן בכל שינוי בשורה.
        // EF Core מוסיף אותה לתנאי ה-WHERE של כל UPDATE ו-DELETE על המפגש.
        builder.Property(s => s.RowVersion).IsRowVersion();

        builder.HasIndex(s => s.StartsAtUtc);
        builder.HasIndex(s => new { s.WorkshopId, s.StartsAtUtc });

        // one-to-many: מפגש -> הרשמות
        builder.HasMany(s => s.Registrations)
            .WithOne(r => r.Session)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
