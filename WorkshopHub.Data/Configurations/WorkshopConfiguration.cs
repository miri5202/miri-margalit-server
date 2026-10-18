using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Data.Configurations;

public class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.ToTable("Workshops");

        builder.Property(w => w.Title).IsRequired().HasMaxLength(120);
        builder.Property(w => w.Description).IsRequired().HasMaxLength(2000);
        builder.Property(w => w.InstructorName).IsRequired().HasMaxLength(100);

        builder.HasIndex(w => w.Title);

        // one-to-many: סדנה -> מפגשים. מחיקת סדנה מוחקת את המפגשים שלה.
        builder.HasMany(w => w.Sessions)
            .WithOne(s => s.Workshop)
            .HasForeignKey(s => s.WorkshopId)
            .OnDelete(DeleteBehavior.Cascade);

        // many-to-many: סדנאות <-> תגיות, דרך טבלת הקישור WorkshopTags (EF Core יוצר אותה בעצמו)
        builder.HasMany(w => w.Tags)
            .WithMany(t => t.Workshops)
            .UsingEntity(join => join.ToTable("WorkshopTags"));
    }
}
