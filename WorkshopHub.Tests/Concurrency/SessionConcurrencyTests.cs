using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Data;
using WorkshopHub.Data.Repositories;
using WorkshopHub.Service.Services;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Concurrency;

/// <summary>
/// ההוכחה שהטיפול בתחרות על המשאב עובד – מול SQL Server אמיתי, עם DbContext נפרד לכל "בקשה".
/// </summary>
[Collection(SqlServerCollection.Name)]
[Trait("Category", "Concurrency")]
public class SessionConcurrencyTests
{
    private readonly SqlServerFixture _database;

    public SessionConcurrencyTests(SqlServerFixture database)
    {
        _database = database;
    }

    // ------------------------------------------------------------------
    //  1. הבדיקה שנדרשת במדריך: שני DbContext, אותה ישות, הראשון שומר והשני נכשל
    // ------------------------------------------------------------------

    [Fact]
    public async Task TwoDbContexts_ChangeTheSameSession_FirstSaveSucceeds_SecondThrowsDbUpdateConcurrencyException()
    {
        // Arrange: מפגש עם מקום אחד פנוי
        var sessionId = await _database.SeedSessionAsync(capacity: 1);

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();

        // שני ה-DbContext קוראים את אותה שורה. שניהם רואים 0 מתוך 1 – מקום פנוי.
        var seenByFirst = await firstContext.Sessions.SingleAsync(s => s.Id == sessionId);
        var seenBySecond = await secondContext.Sessions.SingleAsync(s => s.Id == sessionId);

        Assert.Equal(0, seenByFirst.SeatsTaken);
        Assert.Equal(0, seenBySecond.SeatsTaken);
        Assert.Equal(seenByFirst.RowVersion, seenBySecond.RowVersion);
        var rowVersionBefore = seenByFirst.RowVersion.ToArray();

        // שניהם תופסים את המקום
        seenByFirst.SeatsTaken++;
        seenBySecond.SeatsTaken++;

        // Act + Assert: הראשון שומר בהצלחה...
        var rowsAffected = await firstContext.SaveChangesAsync();
        Assert.Equal(1, rowsAffected);

        // ...והשני נכשל: ה-UPDATE שלו כולל WHERE RowVersion = <הערך שקרא>, והערך כבר השתנה
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());

        // בבסיס הנתונים: מקום אחד תפוס – לא שניים – וה-rowversion התקדם
        await using var verifyContext = _database.CreateContext();
        var stored = await verifyContext.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);

        Assert.Equal(1, stored.SeatsTaken);
        Assert.NotEqual(rowVersionBefore, stored.RowVersion);
    }

    [Fact]
    public async Task UnitOfWork_TranslatesTheEfExceptionIntoConcurrencyConflictException()
    {
        // שכבת ה-Service לא מכירה את EF Core; היא תופסת את החריגה של Core ששכבת ה-Data זורקת
        var sessionId = await _database.SeedSessionAsync(capacity: 1);

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();

        var seenByFirst = await new SessionRepository(firstContext).GetForUpdateAsync(sessionId);
        var seenBySecond = await new SessionRepository(secondContext).GetForUpdateAsync(sessionId);

        seenByFirst!.SeatsTaken++;
        seenBySecond!.SeatsTaken++;

        await new UnitOfWork(firstContext).SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => new UnitOfWork(secondContext).SaveChangesAsync());

        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
    }

    // ------------------------------------------------------------------
    //  2. אותו תרחיש דרך שכבת ה-Service: שתי משתתפות על המקום האחרון
    // ------------------------------------------------------------------

    [Fact]
    public async Task TwoUsers_RegisterForTheLastSeatAtTheSameMoment_ExactlyOneIsConfirmed_TheOtherGetsConflict()
    {
        // Arrange: מקום אחד, שתי משתתפות, ושער שמבטיח ששתיהן קראו את המפגש לפני שמישהי שמרה
        var sessionId = await _database.SeedSessionAsync(capacity: 1);
        var userIds = await _database.SeedUsersAsync(2);
        var gate = new SaveGate(participants: 2);

        await using var firstContext = _database.CreateContext();
        await using var secondContext = _database.CreateContext();

        var firstService = CreateRegistrationService(firstContext, gate);
        var secondService = CreateRegistrationService(secondContext, gate);

        // Act: שתי הבקשות רצות במקביל
        var results = await Task.WhenAll(
            Task.Run(() => firstService.RegisterAsync(sessionId, new RegistrationCreateRequest(), new CurrentUser(userIds[0], UserRole.Member))),
            Task.Run(() => secondService.RegisterAsync(sessionId, new RegistrationCreateRequest(), new CurrentUser(userIds[1], UserRole.Member))));

        // Assert: "שתיהן פעלו בו-זמנית, ואסור היה ששתיהן יקבלו אישור" – ואכן רק אחת קיבלה
        var winner = Assert.Single(results, r => r.IsSuccess);
        var loser = Assert.Single(results, r => r.IsFailure);

        Assert.Equal(RegistrationStatus.Confirmed, winner.Value.Status);
        Assert.Equal(ErrorType.Conflict, loser.Error!.Type);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, loser.Error.Code);

        await using var verifyContext = _database.CreateContext();
        var stored = await verifyContext.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        var storedRegistrations = await verifyContext.Registrations.AsNoTracking().Where(r => r.SessionId == sessionId).ToListAsync();

        // המפסידה לא השאירה עקבות: לא מקום שני, ולא הרשמה "יתומה" (כל השמירה שלה בוטלה)
        Assert.Equal(1, stored.SeatsTaken);
        var storedRegistration = Assert.Single(storedRegistrations);
        Assert.Equal(winner.Value.Id, storedRegistration.Id);
        Assert.Equal(RegistrationStatus.Confirmed, storedRegistration.Status);
    }

    [Fact]
    public async Task ManyUsers_RaceForAFewSeats_TheSessionIsNeverOversold()
    {
        // Arrange: 3 מקומות, 12 משתתפות שנרשמות בבת אחת (בלי שער – מרוץ אמיתי)
        const int capacity = 3;
        const int users = 12;
        var sessionId = await _database.SeedSessionAsync(capacity);
        var userIds = await _database.SeedUsersAsync(users);

        // Act
        var results = await Task.WhenAll(userIds.Select(userId => Task.Run(async () =>
        {
            await using var context = _database.CreateContext();
            return await CreateRegistrationService(context).RegisterAsync(
                sessionId, new RegistrationCreateRequest(), new CurrentUser(userId, UserRole.Member));
        })));

        // Assert
        var confirmed = results.Count(r => r.IsSuccess);
        Assert.InRange(confirmed, 1, capacity);

        // כל מי שלא קיבלה מקום קיבלה 409 מסודר – התנגשות או "מלא" – ולא שגיאה אחרת
        Assert.All(results.Where(r => r.IsFailure), r =>
        {
            Assert.Equal(ErrorType.Conflict, r.Error!.Type);
            Assert.Contains(r.Error.Code, new[] { ErrorCodes.ConcurrencyConflict, ErrorCodes.SessionFull });
        });

        await using var verifyContext = _database.CreateContext();
        var stored = await verifyContext.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        var storedConfirmed = await verifyContext.Registrations
            .AsNoTracking()
            .CountAsync(r => r.SessionId == sessionId && r.Status == RegistrationStatus.Confirmed);

        // המונה, ההרשמות בפועל ומספר האישורים שחזרו ללקוחות – שלושתם זהים, ואף פעם לא מעל הקיבולת
        Assert.Equal(confirmed, stored.SeatsTaken);
        Assert.Equal(confirmed, storedConfirmed);
        Assert.True(stored.SeatsTaken <= capacity);
    }

    // ------------------------------------------------------------------
    //  3. ה-token עושה סיבוב דרך הלקוח: עריכת מפגש עם RowVersion שהתיישן
    // ------------------------------------------------------------------

    [Fact]
    public async Task UpdatingASession_WithTheRowVersionLoadedBeforeSomeoneRegistered_IsRejected_ThenSucceedsWithTheFreshOne()
    {
        var sessionId = await _database.SeedSessionAsync(capacity: 5);
        var userIds = await _database.SeedUsersAsync(1);

        // המנהלת טוענת את טופס העריכה ומקבלת את ה-RowVersion הנוכחי
        byte[] rowVersionLoadedByAdmin;
        DateTime startsAtUtc;
        await using (var context = _database.CreateContext())
        {
            var loaded = await CreateSessionService(context).GetByIdAsync(sessionId, currentUser: null);
            rowVersionLoadedByAdmin = loaded.Value.RowVersion;
            startsAtUtc = loaded.Value.StartsAtUtc;

            // תוך כדי כך בודקים שהתאריך חוזר מבסיס הנתונים מסומן כ-UTC
            Assert.Equal(DateTimeKind.Utc, startsAtUtc.Kind);
        }

        // בינתיים משתתפת נרשמת – שורת המפגש משתנה, וה-RowVersion מתקדם
        await using (var context = _database.CreateContext())
        {
            var registration = await CreateRegistrationService(context).RegisterAsync(
                sessionId, new RegistrationCreateRequest(), new CurrentUser(userIds[0], UserRole.Member));
            Assert.True(registration.IsSuccess);
        }

        // המנהלת שומרת עם ה-RowVersion הישן -> נדחה, והשינוי של המשתתפת לא נדרס
        await using (var context = _database.CreateContext())
        {
            var stale = await CreateSessionService(context).UpdateAsync(sessionId, new SessionUpdateRequest
            {
                StartsAtUtc = startsAtUtc,
                DurationMinutes = 90,
                Location = "אולם הרצאות",
                Capacity = 10,
                RowVersion = rowVersionLoadedByAdmin
            });

            Assert.True(stale.IsFailure);
            Assert.Equal(ErrorCodes.ConcurrencyConflict, stale.Error!.Code);
        }

        // אחרי טעינה מחדש (RowVersion עדכני) אותו עדכון מצליח
        await using (var context = _database.CreateContext())
        {
            var service = CreateSessionService(context);
            var fresh = await service.GetByIdAsync(sessionId, currentUser: null);
            Assert.NotEqual(rowVersionLoadedByAdmin, fresh.Value.RowVersion);

            var updated = await service.UpdateAsync(sessionId, new SessionUpdateRequest
            {
                StartsAtUtc = startsAtUtc,
                DurationMinutes = 90,
                Location = "אולם הרצאות",
                Capacity = 10,
                RowVersion = fresh.Value.RowVersion
            });

            Assert.True(updated.IsSuccess);
            Assert.Equal(10, updated.Value.Capacity);
            Assert.Equal(1, updated.Value.SeatsTaken);
        }
    }

    // ------------------------------------------------------------------
    //  4. רשימת ההמתנה וקווי ההגנה של בסיס הנתונים
    // ------------------------------------------------------------------

    [Fact]
    public async Task CancellingAConfirmedRegistration_PromotesTheOldestWaitlistedUser_AndKeepsTheSessionFull()
    {
        var sessionId = await _database.SeedSessionAsync(capacity: 1);
        var userIds = await _database.SeedUsersAsync(3);
        var first = new CurrentUser(userIds[0], UserRole.Member);
        var second = new CurrentUser(userIds[1], UserRole.Member);
        var third = new CurrentUser(userIds[2], UserRole.Member);
        var joinWaitlist = new RegistrationCreateRequest { JoinWaitlistIfFull = true };

        var confirmed = await RegisterAsync(sessionId, first, joinWaitlist);
        var waitingFirst = await RegisterAsync(sessionId, second, joinWaitlist);
        var waitingSecond = await RegisterAsync(sessionId, third, joinWaitlist);

        Assert.Equal(RegistrationStatus.Confirmed, confirmed.Value.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, waitingFirst.Value.Status);
        Assert.Equal(RegistrationStatus.Waitlisted, waitingSecond.Value.Status);

        // Act: בעלת המקום מבטלת
        await using (var context = _database.CreateContext())
        {
            var cancel = await CreateRegistrationService(context).CancelAsync(confirmed.Value.Id, first);
            Assert.True(cancel.IsSuccess);
        }

        // Assert: הראשונה בתור קיבלה את המקום, השנייה עדיין ממתינה
        await using var verifyContext = _database.CreateContext();
        var stored = await verifyContext.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        var statuses = await verifyContext.Registrations
            .AsNoTracking()
            .Where(r => r.SessionId == sessionId)
            .ToDictionaryAsync(r => r.Id, r => r);

        Assert.Equal(1, stored.SeatsTaken);
        Assert.Equal(1, stored.WaitlistCount);
        Assert.Equal(RegistrationStatus.Cancelled, statuses[confirmed.Value.Id].Status);
        Assert.Equal(RegistrationStatus.Confirmed, statuses[waitingFirst.Value.Id].Status);
        Assert.Equal(RegistrationStatus.Waitlisted, statuses[waitingSecond.Value.Id].Status);
        Assert.NotNull(statuses[waitingFirst.Value.Id].PromotedAtUtc);
        Assert.Equal(DateTimeKind.Utc, statuses[waitingFirst.Value.Id].PromotedAtUtc!.Value.Kind);

        // אחרי ביטול אפשר להירשם שוב (האינדקס הייחודי מתעלם מהרשמות שבוטלו) – הפעם להמתנה
        var again = await RegisterAsync(sessionId, first, joinWaitlist);
        Assert.Equal(RegistrationStatus.Waitlisted, again.Value.Status);
    }

    [Fact]
    public async Task Database_RejectsASecondActiveRegistrationOfTheSameUser_EvenIfTheCodeCheckIsBypassed()
    {
        var sessionId = await _database.SeedSessionAsync(capacity: 5);
        var userIds = await _database.SeedUsersAsync(1);

        await using var context = _database.CreateContext();
        context.Registrations.Add(NewRegistration(sessionId, userIds[0]));
        await context.SaveChangesAsync();

        // עוקפים את ה-Service ומכניסים הרשמה פעילה שנייה ישירות – האינדקס הייחודי המסונן עוצר אותה
        context.Registrations.Add(NewRegistration(sessionId, userIds[0]));

        await Assert.ThrowsAsync<UniqueConstraintException>(() => new UnitOfWork(context).SaveChangesAsync());
    }

    [Fact]
    public async Task Database_RejectsMoreSeatsTakenThanCapacity_EvenIfTheCodeCheckIsBypassed()
    {
        var sessionId = await _database.SeedSessionAsync(capacity: 2);

        await using var context = _database.CreateContext();
        var session = await context.Sessions.SingleAsync(s => s.Id == sessionId);

        // באג היפותטי בקוד שמנסה לשמור 3 נרשמות ב-2 מקומות – ה-CHECK constraint עוצר אותו
        session.SeatsTaken = 3;

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private async Task<Result<RegistrationResponse>> RegisterAsync(int sessionId, CurrentUser user, RegistrationCreateRequest request)
    {
        await using var context = _database.CreateContext();
        var result = await CreateRegistrationService(context).RegisterAsync(sessionId, request, user);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result;
    }

    private static Registration NewRegistration(int sessionId, int userId) => new()
    {
        SessionId = sessionId,
        UserId = userId,
        Status = RegistrationStatus.Confirmed,
        CreatedAtUtc = DateTime.UtcNow
    };

    /// <summary>Service אמיתי עם repositories אמיתיים, על DbContext משלו – כמו בקשת HTTP אחת.</summary>
    private static RegistrationService CreateRegistrationService(AppDbContext context, SaveGate? gate = null)
    {
        IUnitOfWork unitOfWork = new UnitOfWork(context);
        if (gate is not null)
            unitOfWork = new GatedUnitOfWork(unitOfWork, gate);

        return new RegistrationService(
            new SessionRepository(context),
            new RegistrationRepository(context),
            unitOfWork,
            TestMapper.Instance,
            TimeProvider.System,
            NullLogger<RegistrationService>.Instance);
    }

    private static SessionService CreateSessionService(AppDbContext context) =>
        new(
            new SessionRepository(context),
            new WorkshopRepository(context),
            new RegistrationRepository(context),
            new UnitOfWork(context),
            TestMapper.Instance,
            TimeProvider.System,
            NullLogger<SessionService>.Instance);
}
