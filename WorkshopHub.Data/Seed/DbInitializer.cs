using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Data.Seed;

/// <summary>
/// מכין את בסיס הנתונים בעליית השרת, כך שהמערכת רצה מיד אחרי clone – בלי שום פעולה ידנית בבסיס הנתונים:
/// מחיל את ה-migrations (ויוצר את בסיס הנתונים אם אינו קיים), ומכניס נתוני דמו אם הטבלאות ריקות.
/// </summary>
public class DbInitializer : IDbInitializer
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _clock;
    private readonly ILogger<DbInitializer> _logger;

    public DbInitializer(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        TimeProvider clock,
        ILogger<DbInitializer> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task InitializeAsync(bool applyMigrations, bool seedDemoData, CancellationToken cancellationToken = default)
    {
        if (applyMigrations)
        {
            _logger.LogInformation("Applying pending database migrations");
            await _context.Database.MigrateAsync(cancellationToken);
        }

        if (seedDemoData)
            await SeedAsync(cancellationToken);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        // idempotent: אם כבר יש משתמשים, בסיס הנתונים אותחל בעבר ולא נוגעים בו
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            _logger.LogDebug("Database already contains data - skipping demo seed");
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        // ---------- משתמשים ----------
        var admin = new User
        {
            FullName = "מנהלת המערכת",
            Email = DemoData.AdminEmail,
            PasswordHash = _passwordHasher.Hash(DemoData.AdminPassword),
            Role = UserRole.Admin,
            CreatedAtUtc = now
        };

        var members = DemoData.Members
            .Select(m => new User
            {
                FullName = m.FullName,
                Email = m.Email,
                PasswordHash = _passwordHasher.Hash(DemoData.MemberPassword),
                Role = UserRole.Member,
                CreatedAtUtc = now
            })
            .ToList();

        var rachel = members[0];
        var sara = members[1];
        var chana = members[2];
        var leah = members[3];

        // ---------- קטגוריות ותגיות ----------
        var baking = new Category { Name = "אפייה ובישול" };
        var crafts = new Category { Name = "מלאכת יד ויצירה" };
        var computers = new Category { Name = "מחשבים וטכנולוגיה" };
        var management = new Category { Name = "ניהול ופיתוח אישי" };

        var beginners = new Tag { Name = "למתחילות" };
        var advanced = new Tag { Name = "למתקדמות" };
        var evening = new Tag { Name = "ערב" };
        var morning = new Tag { Name = "בוקר" };
        var materials = new Tag { Name = "כולל חומרים" };
        var series = new Tag { Name = "סדרת מפגשים" };

        // ---------- סדנאות ומפגשים ----------
        // מועדי המפגשים יחסיים ליום האתחול, כדי שתמיד יהיו מפגשים עתידיים להדגמה
        var today = now.Date;
        DateTime At(int daysFromToday, int hourUtc) => DateTime.SpecifyKind(today.AddDays(daysFromToday).AddHours(hourUtc), DateTimeKind.Utc);

        Session NewSession(int daysFromToday, int hourUtc, int durationMinutes, string location, int capacity) => new()
        {
            StartsAtUtc = At(daysFromToday, hourUtc),
            DurationMinutes = durationMinutes,
            Location = location,
            Capacity = capacity
        };

        var challah = new Workshop
        {
            Title = "אפיית חלות ולחמים",
            Description = "לשים, קולעים ואופים: בצק שמרים מאפס, שש שיטות קליעה וטיפים לחלה רכה שנשארת טרייה.",
            InstructorName = "דבורה קליין",
            Category = baking,
            Tags = { beginners, materials, evening },
            Sessions =
            {
                NewSession(3, 16, 180, "מטבח הלימוד, קומה 1", 3),
                NewSession(10, 16, 180, "מטבח הלימוד, קומה 1", 8),
                NewSession(24, 16, 180, "מטבח הלימוד, קומה 1", 8)
            }
        };

        var cakes = new Workshop
        {
            Title = "עוגות מעוצבות לאירועים",
            Description = "עבודה עם בצק סוכר, זילוף קרם וצביעה. כל משתתפת יוצאת עם עוגה מעוצבת.",
            InstructorName = "מלכה רוזן",
            Category = baking,
            Tags = { advanced, materials },
            Sessions =
            {
                NewSession(6, 8, 240, "מטבח הלימוד, קומה 1", 6),
                NewSession(20, 8, 240, "מטבח הלימוד, קומה 1", 6)
            }
        };

        var calligraphy = new Workshop
        {
            Title = "קליגרפיה עברית",
            Description = "היכרות עם הציפורן והדיו, אותיות הכתב המרובע, ועיצוב ברכה או הזמנה בכתב יד.",
            InstructorName = "אסתר וייס",
            Category = crafts,
            Tags = { beginners, series },
            Sessions =
            {
                NewSession(5, 17, 120, "חדר יצירה 2", 2),
                NewSession(12, 17, 120, "חדר יצירה 2", 10),
                NewSession(19, 17, 120, "חדר יצירה 2", 10)
            }
        };

        var sewing = new Workshop
        {
            Title = "תפירה בסיסית במכונה",
            Description = "השחלה, תפרים ישרים וזיגזג, ותפירת תיק בד ראשון. מכונות התפירה במקום.",
            InstructorName = "יעל שטרן",
            Category = crafts,
            Tags = { beginners, morning, materials },
            Sessions =
            {
                NewSession(8, 7, 150, "חדר יצירה 1", 5),
                NewSession(15, 7, 150, "חדר יצירה 1", 5),
                NewSession(-7, 7, 150, "חדר יצירה 1", 5)
            }
        };

        var knitting = new Workshop
        {
            Title = "סריגה למתחילות",
            Description = "העלאת עיניים, עין ימין ועין שמאל, וסריגת צעיף ראשון בקצב אישי.",
            InstructorName = "פנינה זילבר",
            Category = crafts,
            Tags = { beginners, evening },
            Sessions =
            {
                NewSession(9, 17, 90, "חדר יצירה 2", 12),
                NewSession(30, 17, 90, "חדר יצירה 2", 12)
            }
        };

        var excel = new Workshop
        {
            Title = "יסודות Excel למשרד",
            Description = "נוסחאות, טבלאות, סינון ומיון, וטבלת ציר ראשונה – על קבצים אמיתיים מעבודת משרד.",
            InstructorName = "נחמה הורוביץ",
            Category = computers,
            Tags = { beginners, evening },
            Sessions =
            {
                NewSession(7, 16, 120, "מעבדת מחשבים", 1),
                NewSession(14, 16, 120, "מעבדת מחשבים", 15),
                NewSession(28, 16, 120, "מעבדת מחשבים", 15)
            }
        };

        var csharp = new Workshop
        {
            Title = "מבוא לתכנות ב-C#",
            Description = "משתנים, תנאים, לולאות ופונקציות – ובסוף הסדרה אפליקציית קונסול קטנה משלך.",
            InstructorName = "ברכה אדלר",
            Category = computers,
            Tags = { beginners, series, evening },
            Sessions =
            {
                NewSession(4, 17, 150, "מעבדת מחשבים", 14),
                NewSession(11, 17, 150, "מעבדת מחשבים", 14),
                NewSession(18, 17, 150, "מעבדת מחשבים", 14)
            }
        };

        var design = new Workshop
        {
            Title = "עיצוב מודעות והזמנות ב-Canva",
            Description = "בחירת תבנית, פונטים וצבעים, והכנת קובץ מוכן להדפסה.",
            InstructorName = "גיטי מאיר",
            Category = computers,
            Tags = { beginners, morning },
            Sessions =
            {
                NewSession(13, 8, 120, "מעבדת מחשבים", 10),
                NewSession(27, 8, 120, "מעבדת מחשבים", 10)
            }
        };

        var budget = new Workshop
        {
            Title = "ניהול תקציב משפחתי",
            Description = "מיפוי הכנסות והוצאות, בניית תקציב חודשי שעובד, וכלים למעקב פשוט.",
            InstructorName = "ציפורה לנדאו",
            Category = management,
            Tags = { evening },
            Sessions =
            {
                NewSession(16, 17, 120, "אולם הרצאות", 30),
                NewSession(40, 17, 120, "אולם הרצאות", 30)
            }
        };

        var time = new Workshop
        {
            Title = "ניהול זמן וסדר יום",
            Description = "תכנון שבועי, קביעת סדרי עדיפויות והתמודדות עם עומס – בכלים מעשיים.",
            InstructorName = "שושנה גרין",
            Category = management,
            Tags = { morning, series },
            Sessions =
            {
                NewSession(21, 7, 90, "אולם הרצאות", 20),
                NewSession(35, 7, 90, "אולם הרצאות", 20)
            }
        };

        // ---------- הרשמות ----------
        // מצבים מוכנים להדגמה: מפגש עם מקום אחרון, מפגש מלא עם רשימת המתנה, ומפגש עם מקום יחיד ופנוי.
        var registrations = new List<Registration>();

        void Register(Session session, User user, RegistrationStatus status, int minutesAgo)
        {
            if (status == RegistrationStatus.Confirmed)
                session.SeatsTaken++;
            else if (status == RegistrationStatus.Waitlisted)
                session.WaitlistCount++;

            registrations.Add(new Registration
            {
                Session = session,
                User = user,
                Status = status,
                CreatedAtUtc = now.AddMinutes(-minutesAgo)
            });
        }

        var challahFirst = challah.Sessions.First();          // 3 מקומות, 2 תפוסים -> נשאר מקום אחרון
        Register(challahFirst, rachel, RegistrationStatus.Confirmed, 300);
        Register(challahFirst, sara, RegistrationStatus.Confirmed, 240);

        var calligraphyFirst = calligraphy.Sessions.First();  // 2 מקומות, מלא, אחת ממתינה
        Register(calligraphyFirst, chana, RegistrationStatus.Confirmed, 200);
        Register(calligraphyFirst, leah, RegistrationStatus.Confirmed, 180);
        Register(calligraphyFirst, sara, RegistrationStatus.Waitlisted, 60);

        var csharpFirst = csharp.Sessions.First();
        Register(csharpFirst, rachel, RegistrationStatus.Confirmed, 120);
        Register(csharpFirst, chana, RegistrationStatus.Confirmed, 90);

        var sewingPast = sewing.Sessions.Last();              // מפגש שכבר התקיים
        Register(sewingPast, rachel, RegistrationStatus.Confirmed, 20000);
        Register(sewingPast, leah, RegistrationStatus.Confirmed, 19000);

        // שמירה אחת: EF Core מזהה את כל הגרף (סדנאות -> מפגשים, תגיות, הרשמות) ומכניס אותו בטרנזקציה אחת
        _context.Users.Add(admin);
        _context.Users.AddRange(members);
        _context.Workshops.AddRange(challah, cakes, calligraphy, sewing, knitting, excel, csharp, design, budget, time);
        _context.Registrations.AddRange(registrations);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded demo data: {Users} users, {Workshops} workshops, {Sessions} sessions, {Registrations} registrations",
            members.Count + 1,
            10,
            await _context.Sessions.CountAsync(cancellationToken),
            registrations.Count);
    }
}
