using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.Property(c => c.Name).IsRequired().HasMaxLength(60);
        builder.HasIndex(c => c.Name).IsUnique();

        // one-to-many: קטגוריה -> סדנאות. Restrict: אי אפשר למחוק קטגוריה שיש בה סדנאות.
        builder.HasMany(c => c.Workshops)
            .WithOne(w => w.Category)
            .HasForeignKey(w => w.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
