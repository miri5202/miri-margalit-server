using System.Globalization;
using System.Security.Cryptography;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Security;

/// <summary>
/// גיבוב סיסמאות ב-PBKDF2 (HMAC-SHA256) עם salt אקראי לכל סיסמה.
/// הפורמט שנשמר: PBKDF2-SHA256$iterations$salt$hash – כך אפשר להעלות את מספר האיטרציות בעתיד
/// בלי לשבור סיסמאות קיימות.
/// </summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Algorithm = "PBKDF2-SHA256";
    private const char Separator = '$';
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

        return string.Join(
            Separator,
            Algorithm,
            Iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(key));
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(passwordHash))
            return false;

        var parts = passwordHash.Split(Separator);
        if (parts.Length != 4
            || parts[0] != Algorithm
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expectedKey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expectedKey = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);

        // השוואה בזמן קבוע, כדי שמשך הבדיקה לא ידליף כמה בתים מהגיבוב תואמים
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}
