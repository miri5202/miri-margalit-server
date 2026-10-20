using Microsoft.EntityFrameworkCore;
using Npgsql;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Data;

namespace WorkshopHub.Tests.Concurrency;

/// <summary>
/// בסיס נתונים אמיתי לבדיקות ה-concurrency. ה-token הוא xmin – עמודת מערכת ש-PostgreSQL עצמו מעדכן,
/// ולכן בדיקה שמוכיחה אותו חייבת לרוץ מול PostgreSQL – בסיס נתונים בזיכרון לא מעדכן xmin ולא היה מוכיח כלום.
///
/// ה-fixture יוצר schema זמני בשם ייחודי (ולא בסיס נתונים שלם – כך מספיקה הרשאה של בעלת בסיס הנתונים,
/// גם בשירות מנוהל בענן), מחיל עליו את ה-migrations האמיתיות של הפרויקט, ומוחק אותו בסיום.
/// ברירת המחדל היא PostgreSQL מקומי (localhost:5432, משתמש postgres); אפשר לכוון לשרת אחר עם משתנה
/// הסביבה WORKSHOPHUB_TEST_POSTGRES (connection string רגיל או בצורת URL: postgresql://user:password@host/db).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DefaultServer = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";

    private readonly string _serverConnectionString;
    private readonly string _schema = $"tests_{Guid.NewGuid():N}";

    public PostgresFixture()
    {
        var server = Environment.GetEnvironmentVariable("WORKSHOPHUB_TEST_POSTGRES");
        _serverConnectionString = PostgresConnectionString.Normalize(string.IsNullOrWhiteSpace(server) ? DefaultServer : server)!;

        // כל הטבלאות (וגם טבלת ההיסטוריה של ה-migrations) נוצרות ב-schema הראשון ב-search path
        var builder = new NpgsqlConnectionStringBuilder(_serverConnectionString) { SearchPath = _schema };
        ConnectionString = builder.ConnectionString;
    }

    public string ConnectionString { get; }

    /// <summary>
    /// DbContext חדש ונפרד בכל קריאה – בדיוק כמו שכל בקשת HTTP מקבלת DbContext משלה (scoped).
    /// </summary>
    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await ExecuteOnServerAsync($"CREATE SCHEMA \"{_schema}\"");

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => ExecuteOnServerAsync($"DROP SCHEMA IF EXISTS \"{_schema}\" CASCADE");

    private async Task ExecuteOnServerAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_serverConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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
[CollectionDefinition(PostgresCollection.Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "PostgreSQL";
}
