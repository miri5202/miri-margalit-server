using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Service.Services;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Unit;

/// <summary>
/// בדיקות יחידה ללוגיקה של המשאב המוגבל (מקום במפגש), עם repositories מדומים ב-Moq.
/// נבדקים גם המקרה התקין (המקום נתפס) וגם המקרים שנדחים (מפגש מלא, התנגשות עם בקשה אחרת).
/// </summary>
public class RegistrationServiceTests
{
    private const int SessionId = 10;

    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly CurrentUser Member = new(7, UserRole.Member);
    private static readonly CurrentUser OtherMember = new(8, UserRole.Member);
    private static readonly CurrentUser Admin = new(1, UserRole.Admin);

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IRegistrationRepository> _registrations = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly RegistrationService _service;

    private Registration? _addedRegistration;

    public RegistrationServiceTests()
    {
        _registrations
            .Setup(r => r.AddAsync(It.IsAny<Registration>(), It.IsAny<CancellationToken>()))
            .Callback<Registration, CancellationToken>((registration, _) => _addedRegistration = registration)
            .Returns(Task.CompletedTask);

        _registrations
            .Setup(r => r.GetNextWaitlistedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Registration>());

        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _service = new RegistrationService(
            _sessions.Object,
            _registrations.Object,
            _unitOfWork.Object,
            TestMapper.Instance,
            new FixedTimeProvider(Now),
            NullLogger<RegistrationService>.Instance);
    }

    // ------------------------------------------------------------------
    //  הרשמה
    // ------------------------------------------------------------------

    [Fact]
    public async Task RegisterAsync_WhenSeatIsAvailable_ConfirmsRegistrationAndTakesTheSeat()
    {
        // Arrange: מפגש עם 3 מקומות, 2 תפוסים – נשאר מקום אחרון
        var session = GivenSession(capacity: 3, seatsTaken: 2);

        // Act
        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Confirmed, result.Value.Status);
        Assert.Equal(3, session.SeatsTaken);
        Assert.True(session.IsFull);

        Assert.NotNull(_addedRegistration);
        Assert.Equal(Member.Id, _addedRegistration!.UserId);
        Assert.Equal(SessionId, _addedRegistration.SessionId);
        Assert.Equal(Now, _addedRegistration.CreatedAtUtc);

        // ההרשמה ועדכון המונה נשמרים בשמירה אחת (טרנזקציה אחת)
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenSessionIsFull_ReturnsConflictAndSavesNothing()
    {
        var session = GivenSession(capacity: 2, seatsTaken: 2);

        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.SessionFull, result.Error.Code);

        // המונה לא זז, לא נוספה הרשמה ולא בוצעה שמירה
        Assert.Equal(2, session.SeatsTaken);
        Assert.Equal(0, session.WaitlistCount);
        _registrations.Verify(r => r.AddAsync(It.IsAny<Registration>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenSessionIsFullAndWaitlistRequested_JoinsTheWaitlistWithoutTakingASeat()
    {
        var session = GivenSession(capacity: 2, seatsTaken: 2);

        var result = await _service.RegisterAsync(
            SessionId, new RegistrationCreateRequest { JoinWaitlistIfFull = true }, Member, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Waitlisted, result.Value.Status);
        Assert.Equal(2, session.SeatsTaken);
        Assert.Equal(1, session.WaitlistCount);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenAnotherRequestTookTheSeatBetweenReadAndSave_ReturnsConcurrencyConflict()
    {
        // Arrange: בזמן הקריאה היה מקום פנוי, אבל עד השמירה בקשה אחרת שינתה את המפגש.
        // שכבת ה-Data מדווחת על כך בחריגה ConcurrencyConflictException.
        GivenSession(capacity: 1, seatsTaken: 0);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("row changed"));

        // Act
        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        // Assert: לא חריגה ולא 500 – כישלון עסקי מסוג Conflict, שה-API מתרגם ל-409
        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.Error.Code);
    }

    [Fact]
    public async Task RegisterAsync_WhenUserIsAlreadyRegistered_ReturnsConflict()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 1);
        _registrations
            .Setup(r => r.HasActiveAsync(SessionId, Member.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AlreadyRegistered, result.Error!.Code);
        Assert.Equal(1, session.SeatsTaken);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenUniqueIndexRejectsADuplicate_ReturnsAlreadyRegistered()
    {
        // אותה משתמשת שלחה שתי בקשות במקביל: שתיהן עברו את הבדיקה, והאינדקס הייחודי עצר את השנייה
        GivenSession(capacity: 5, seatsTaken: 1);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintException("duplicate"));

        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.AlreadyRegistered, result.Error.Code);
    }

    [Fact]
    public async Task RegisterAsync_WhenSessionAlreadyStarted_ReturnsConflict()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 0, startsAtUtc: Now.AddMinutes(-1));

        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.SessionAlreadyStarted, result.Error!.Code);
        Assert.Equal(0, session.SeatsTaken);
    }

    [Fact]
    public async Task RegisterAsync_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        _sessions
            .Setup(r => r.GetForUpdateAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _service.RegisterAsync(SessionId, new RegistrationCreateRequest(), Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    // ------------------------------------------------------------------
    //  ביטול
    // ------------------------------------------------------------------

    [Fact]
    public async Task CancelAsync_ConfirmedRegistration_FreesTheSeat()
    {
        var session = GivenSession(capacity: 3, seatsTaken: 3);
        var registration = GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Confirmed);

        var result = await _service.CancelAsync(50, Member, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Cancelled, registration.Status);
        Assert.Equal(Now, registration.CancelledAtUtc);
        Assert.Equal(2, session.SeatsTaken);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_WhenSomeoneIsWaiting_GivesTheFreedSeatToTheNextInLine()
    {
        var session = GivenSession(capacity: 2, seatsTaken: 2, waitlistCount: 1);
        var cancelled = GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Confirmed);
        var waiting = new Registration
        {
            Id = 51,
            SessionId = SessionId,
            Session = session,
            UserId = OtherMember.Id,
            Status = RegistrationStatus.Waitlisted,
            CreatedAtUtc = Now.AddHours(-1)
        };
        _registrations
            .Setup(r => r.GetNextWaitlistedAsync(SessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { waiting });

        var result = await _service.CancelAsync(50, Member, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Cancelled, cancelled.Status);

        // הממתינה קודמה, והמפגש נשאר מלא – המקום לא "נעלם" ולא הוכפל
        Assert.Equal(RegistrationStatus.Confirmed, waiting.Status);
        Assert.Equal(Now, waiting.PromotedAtUtc);
        Assert.Equal(2, session.SeatsTaken);
        Assert.Equal(0, session.WaitlistCount);
    }

    [Fact]
    public async Task CancelAsync_WaitlistedRegistration_OnlyShortensTheWaitlist()
    {
        var session = GivenSession(capacity: 2, seatsTaken: 2, waitlistCount: 1);
        GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Waitlisted);

        var result = await _service.CancelAsync(50, Member, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, session.SeatsTaken);
        Assert.Equal(0, session.WaitlistCount);
        _registrations.Verify(
            r => r.GetNextWaitlistedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CancelAsync_ByAnotherMember_ReturnsForbiddenAndChangesNothing()
    {
        var session = GivenSession(capacity: 3, seatsTaken: 1);
        var registration = GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Confirmed);

        var result = await _service.CancelAsync(50, OtherMember, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.Equal(1, session.SeatsTaken);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_ByAdmin_CanCancelAnotherUsersRegistration()
    {
        var session = GivenSession(capacity: 3, seatsTaken: 1);
        var registration = GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Confirmed);

        var result = await _service.CancelAsync(50, Admin, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Cancelled, registration.Status);
        Assert.Equal(0, session.SeatsTaken);
    }

    [Fact]
    public async Task CancelAsync_WhenAlreadyCancelled_ReturnsConflict()
    {
        var session = GivenSession(capacity: 3, seatsTaken: 1);
        GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Cancelled);

        var result = await _service.CancelAsync(50, Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.AlreadyCancelled, result.Error!.Code);
        Assert.Equal(1, session.SeatsTaken);
    }

    [Fact]
    public async Task CancelAsync_WhenSessionChangedConcurrently_ReturnsConcurrencyConflict()
    {
        var session = GivenSession(capacity: 3, seatsTaken: 1);
        GivenRegistration(id: 50, session, Member.Id, RegistrationStatus.Confirmed);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("row changed"));

        var result = await _service.CancelAsync(50, Member, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ByAnotherMember_ReturnsForbidden()
    {
        var session = GivenSession(capacity: 3, seatsTaken: 1);
        var registration = new Registration { Id = 50, SessionId = SessionId, Session = session, UserId = Member.Id, Status = RegistrationStatus.Confirmed };
        _registrations
            .Setup(r => r.GetDetailsAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        var result = await _service.GetByIdAsync(50, OtherMember, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Forbidden, result.Error!.Type);
    }

    // ------------------------------------------------------------------
    //  Arrange helpers
    // ------------------------------------------------------------------

    private Session GivenSession(int capacity, int seatsTaken, int waitlistCount = 0, DateTime? startsAtUtc = null)
    {
        var session = new Session
        {
            Id = SessionId,
            WorkshopId = 3,
            Workshop = new Workshop { Id = 3, Title = "קליגרפיה עברית", InstructorName = "אסתר וייס" },
            StartsAtUtc = startsAtUtc ?? Now.AddDays(7),
            DurationMinutes = 120,
            Location = "חדר יצירה 2",
            Capacity = capacity,
            SeatsTaken = seatsTaken,
            WaitlistCount = waitlistCount
        };

        _sessions
            .Setup(r => r.GetForUpdateAsync(SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return session;
    }

    private Registration GivenRegistration(int id, Session session, int userId, RegistrationStatus status)
    {
        var registration = new Registration
        {
            Id = id,
            SessionId = session.Id,
            Session = session,
            UserId = userId,
            Status = status,
            CreatedAtUtc = Now.AddDays(-1)
        };

        _registrations
            .Setup(r => r.GetForUpdateAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        return registration;
    }
}
