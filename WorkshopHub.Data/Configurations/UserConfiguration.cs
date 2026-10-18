using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkshopHub.Core.Entities;

namespace WorkshopHub.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.Property(u => u.FullName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);

        // ה-enum נשמר כטקסט (Member / Admin) כדי שהטבלה תהיה קריאה גם ב-SSMS
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        // ההגנה האמיתית מפני שני חשבונות עם אותו מייל היא האינדקס הייחודי, לא הבדיקה בקוד
        builder.HasIndex(u => u.Email).IsUnique();
    }
}
