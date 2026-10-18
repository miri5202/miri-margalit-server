using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Data;

namespace WorkshopHub.Tests.Concurrency;

/// <summary>
/// בסיס נתונים אמיתי לבדיקות ה-concurrency. rowversion הוא מנגנון של SQL Server עצמו, ולכן בדיקה שמוכיחה
/// אותו חייבת לרוץ מול SQL Server – בסיס נתונים בזיכרון לא מעדכן rowversion ולא היה מוכיח כלום.
///
/// ה-fixture יוצר בסיס נתונים זמני בשם ייחודי, מחיל עליו את ה-migrations האמיתיות של הפרויקט,
/// ומוחק אותו בסיום. ברירת המחדל היא LocalDB; אפשר לכוון לשרת אחר עם משתנה הסביבה
/// WORKSHOPHUB_TEST_SQLSERVER (connection string בלי שם בסיס נתונים).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DefaultServer = @"Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    public SqlServerFixture()
    {
        var server = Environment.GetEnvironmentVariable("WORKSHOPHUB_TEST_SQLSERVER");

        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)
        {
            InitialCatalog = $"WorkshopHub_Tests_{Guid.NewGuid():N}"
        };

        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    /// <summary>
    /// DbContext חדש ונפרד בכל קריאה – בדיוק כמו שכל בקשת HTTP מקבלת DbContext משלה (scoped).
    /// </summary>
    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    /// <summary>יוצר סדנה עם מפגש עתידי אחד ומחזיר את מזהה המפגש.</summary>
    public async Task<int> SeedSessionAsync(int capacity)
    {
        await using var context = CreateContext();
        var unique = Guid.NewGuid().ToString("N");

        var session = new Session
        {
            StartsAtUtc = DateTime.UtcNow.AddDays(7),
            DurationMinutes = 120,
            Location = "חדר בדיקות",
            Capacity = capacity,
            Workshop = new Workshop
            {
                Title = $"סדנת בדיקה {unique}",
                Description = "נוצרה על ידי בדיקה אוטומטית",
                InstructorName = "מנחת בדיקות",
                Category = new Category { Name = $"קטגוריה {unique}" }
            }
        };

        context.Sessions.Add(session);
        await context.SaveChangesAsync();
        return session.Id;
    }

    /// <summary>יוצר משתמשות ומחזיר את המזהים שלהן.</summary>
    public async Task<IReadOnlyList<int>> SeedUsersAsync(int count)
    {
        await using var context = CreateContext();

        var users = Enumerable.Range(0, count)
            .Select(_ => new User
            {
                FullName = "משתתפת בדיקה",
                Email = $"{Guid.NewGuid():N}@tests.local",
                PasswordHash = "not-a-real-hash",
                Role = UserRole.Member,
                CreatedAtUtc = DateTime.UtcNow
            })
            .ToList();

        context.Users.AddRange(users);
        await context.SaveChangesAsync();
        return users.Select(u => u.Id).ToList();
    }
}

/// <summary>כל בדיקות ה-concurrency חולקות את אותו בסיס נתונים זמני.</summary>
[CollectionDefinition(SqlServerCollection.Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
