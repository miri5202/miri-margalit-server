using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Entities;
using WorkshopHub.Data.Converters;

namespace WorkshopHub.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Workshop> Workshops => Set<Workshop>();

    public DbSet<Session> Sessions => Set<Session>();

    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // כל הגדרות ה-Fluent API יושבות במחלקות IEntityTypeConfiguration נפרדות בתיקיית Configurations
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // חל על כל מאפיין מסוג DateTime (וגם DateTime?) בכל הישויות
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }
}
