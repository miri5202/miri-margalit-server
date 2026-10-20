using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;

namespace WorkshopHub.Data.Configurations;

public class RegistrationConfiguration : IEntityTypeConfiguration<Registration>
{
    public void Configure(EntityTypeBuilder<Registration> builder)
    {
        builder.ToTable("Registrations");

        // ה-enum נשמר כטקסט (Confirmed / Waitlisted / Cancelled)
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        // one-to-many: משתמשת -> הרשמות. Restrict: לא מוחקים משתמשת שיש לה היסטוריית הרשמות.
        builder.HasOne(r => r.User)
            .WithMany(u => u.Registrations)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // אינדקס ייחודי מסונן: למשתמשת יכולה להיות רק הרשמה פעילה אחת לכל מפגש.
        // הרשמות שבוטלו לא נכללות, ולכן אפשר להירשם שוב אחרי ביטול.
        builder.HasIndex(r => new { r.SessionId, r.UserId })
            .IsUnique()
            .HasFilter($"\"Status\" <> '{nameof(RegistrationStatus.Cancelled)}'")
            .HasDatabaseName("UX_Registrations_Session_User_Active");

        // משרת את שליפת "הממתינה הבאה בתור": לפי מפגש וסטטוס, בסדר ההצטרפות
        builder.HasIndex(r => new { r.SessionId, r.Status, r.CreatedAtUtc })
            .HasDatabaseName("IX_Registrations_Session_Status_CreatedAt");

        builder.HasIndex(r => new { r.UserId, r.CreatedAtUtc });
    }
}
