using Npgsql;

namespace WorkshopHub.Data;

/// <summary>
/// ה-connection string של PostgreSQL יכול להגיע בשתי צורות:
/// <list type="bullet">
/// <item>הצורה של Npgsql: <c>Host=...;Port=5432;Database=...;Username=...;Password=...</c></item>
/// <item>הצורה ש-Render (ושירותי ענן אחרים) מציגים: <c>postgresql://user:password@host:5432/database</c></item>
/// </list>
/// Npgsql מבין רק את הראשונה, ולכן השנייה מומרת אליה בעליית השרת.
/// </summary>
public static class PostgresConnectionString
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            // החיבור החיצוני של Render דורש SSL; Prefer משתמש בו כשהשרת תומך, ולא נכשל כשלא
            SslMode = SslMode.Prefer
        };

        return builder.ConnectionString;
    }
}
