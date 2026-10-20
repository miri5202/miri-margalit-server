using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Service.Services;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Unit;

public class SessionServiceTests
{
    private const int SessionId = 10;
    private const int WorkshopId = 3;

    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private const uint ClientRowVersion = 2001;

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IWorkshopRepository> _workshops = new();
    private readonly Mock<IRegistrationRepository> _registrations = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly SessionService _service;

    public SessionServiceTests()
    {
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _registrations
            .Setup(r => r.GetNextWaitlistedAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Registration>());

        _registrations
            .Setup(r => r.GetActiveByUserForSessionsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Registration>());

        _service = new SessionService(
            _sessions.Object,
            _workshops.Object,
            _registrations.Object,
            _unitOfWork.Object,
            TestMapper.Instance,
            new FixedTimeProvider(Now),
            NullLogger<SessionService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_WithFutureDate_AddsSessionToTheWorkshop()
    {
        Session? added = null;
        _workshops.Setup(r => r.ExistsAsync(WorkshopId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _sessions
            .Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((session, _) => added = session)
            .Returns(Task.CompletedTask);
        _sessions
            .Setup(r => r.GetDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => added);

        var result = await _service.CreateAsync(WorkshopId, NewCreateRequest(Now.AddDays(3), capacity: 12), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal(WorkshopId, added!.WorkshopId);
        Assert.Equal(12, added.Capacity);
        Assert.Equal(0, added.SeatsTaken);
        Assert.Equal(DateTimeKind.Utc, added.StartsAtUtc.Kind);
        Assert.Equal(12, result.Value.SeatsLeft);
    }

    [Fact]
    public async Task CreateAsync_WithPastDate_ReturnsValidationErrorAndSavesNothing()
    {
        _workshops.Setup(r => r.ExistsAsync(WorkshopId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.CreateAsync(WorkshopId, NewCreateRequest(Now.AddMinutes(-5), capacity: 12), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Equal(ErrorCodes.SessionNotInFuture, result.Error.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenWorkshopDoesNotExist_ReturnsNotFound()
    {
        _workshops.Setup(r => r.ExistsAsync(WorkshopId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CreateAsync(WorkshopId, NewCreateRequest(Now.AddDays(3), capacity: 12), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task UpdateAsync_ChecksTheSaveAgainstTheRowVersionTheClientSaw()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 2);

        var result = await _service.UpdateAsync(SessionId, NewUpdateRequest(session.StartsAtUtc, capacity: 8), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, session.Capacity);

        // ה-token שהלקוח החזיר – ולא זה שהשרת קרא כרגע – הוא שנבדק בשמירה
        _sessions.Verify(r => r.SetOriginalRowVersion(session, ClientRowVersion), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenSessionChangedSinceTheClientLoadedIt_ReturnsConcurrencyConflict()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 2);
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("row changed"));

        var result = await _service.UpdateAsync(SessionId, NewUpdateRequest(session.StartsAtUtc, capacity: 8), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_WhenCapacityIsBelowSeatsTaken_ReturnsConflictAndKeepsCapacity()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 4);

        var result = await _service.UpdateAsync(SessionId, NewUpdateRequest(session.StartsAtUtc, capacity: 3), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.CapacityBelowSeatsTaken, result.Error!.Code);
        Assert.Equal(5, session.Capacity);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WhenCapacityGrows_PromotesWaitingRegistrationsIntoTheNewSeats()
    {
        // מפגש מלא (2/2) עם 3 ממתינות; מגדילים ל-4 מקומות -> 2 הוותיקות מקודמות, אחת נשארת בהמתנה
        var session = GivenSession(capacity: 2, seatsTaken: 2, waitlistCount: 3);
        var first = new Registration { Id = 1, SessionId = SessionId, Status = RegistrationStatus.Waitlisted };
        var second = new Registration { Id = 2, SessionId = SessionId, Status = RegistrationStatus.Waitlisted };
        _registrations
            .Setup(r => r.GetNextWaitlistedAsync(SessionId, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { first, second });

        var result = await _service.UpdateAsync(SessionId, NewUpdateRequest(session.StartsAtUtc, capacity: 4), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Confirmed, first.Status);
        Assert.Equal(RegistrationStatus.Confirmed, second.Status);
        Assert.Equal(Now, first.PromotedAtUtc);
        Assert.Equal(4, session.SeatsTaken);
        Assert.Equal(1, session.WaitlistCount);
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionHasActiveRegistrations_ReturnsConflict()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 1);

        var result = await _service.DeleteAsync(SessionId, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.HasActiveRegistrations, result.Error!.Code);
        _sessions.Verify(r => r.Remove(session), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_WhenSessionIsEmpty_RemovesIt()
    {
        var session = GivenSession(capacity: 5, seatsTaken: 0);

        var result = await _service.DeleteAsync(SessionId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _sessions.Verify(r => r.Remove(session), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ForLoggedInUser_MarksHerRegistrationsUsingASingleQuery()
    {
        var user = new CurrentUser(7, UserRole.Member);
        var sessions = new[]
        {
            NewSession(id: 1, capacity: 5, seatsTaken: 1),
            NewSession(id: 2, capacity: 5, seatsTaken: 5),
            NewSession(id: 3, capacity: 5, seatsTaken: 0)
        };
        _sessions
            .Setup(r => r.SearchAsync(It.IsAny<SessionQuery>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Session>(sessions, page: 1, pageSize: 10, totalCount: 3));
        _registrations
            .Setup(r => r.GetActiveByUserForSessionsAsync(user.Id, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Registration { Id = 100, SessionId = 1, UserId = user.Id, Status = RegistrationStatus.Confirmed },
                new Registration { Id = 101, SessionId = 2, UserId = user.Id, Status = RegistrationStatus.Waitlisted }
            });

        var page = await _service.SearchAsync(new SessionQuery(), user, CancellationToken.None);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(100, page.Items[0].MyRegistrationId);
        Assert.Equal(RegistrationStatus.Confirmed, page.Items[0].MyRegistrationStatus);
        Assert.Equal(RegistrationStatus.Waitlisted, page.Items[1].MyRegistrationStatus);
        Assert.Null(page.Items[2].MyRegistrationId);

        // שאילתה אחת לכל העמוד, ולא שאילתה לכל מפגש (N+1)
        _registrations.Verify(
            r => r.GetActiveByUserForSessionsAsync(user.Id, It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchAsync_ForAnonymousUser_DoesNotQueryRegistrations()
    {
        _sessions
            .Setup(r => r.SearchAsync(It.IsAny<SessionQuery>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<Session>(new[] { NewSession(id: 1, capacity: 5, seatsTaken: 1) }, 1, 10, 1));

        var page = await _service.SearchAsync(new SessionQuery(), currentUser: null, CancellationToken.None);

        Assert.Single(page.Items);
        _registrations.Verify(
            r => r.GetActiveByUserForSessionsAsync(It.IsAny<int>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ------------------------------------------------------------------
    //  Arrange helpers
    // ------------------------------------------------------------------

    private Session GivenSession(int capacity, int seatsTaken, int waitlistCount = 0)
    {
        var session = NewSession(SessionId, capacity, seatsTaken, waitlistCount);

        _sessions.Setup(r => r.GetForUpdateAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        _sessions.Setup(r => r.GetDetailsAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);

        return session;
    }

    private static Session NewSession(int id, int capacity, int seatsTaken, int waitlistCount = 0) => new()
    {
        Id = id,
        WorkshopId = WorkshopId,
        Workshop = new Workshop
        {
            Id = WorkshopId,
            Title = "אפיית חלות ולחמים",
            InstructorName = "דבורה קליין",
            Category = new Category { Id = 1, Name = "אפייה ובישול" }
        },
        StartsAtUtc = Now.AddDays(7),
        DurationMinutes = 180,
        Location = "מטבח הלימוד",
        Capacity = capacity,
        SeatsTaken = seatsTaken,
        WaitlistCount = waitlistCount,
        RowVersion = 2002
    };

    private static SessionCreateRequest NewCreateRequest(DateTime startsAtUtc, int capacity) => new()
    {
        StartsAtUtc = startsAtUtc,
        DurationMinutes = 180,
        Location = "  מטבח הלימוד  ",
        Capacity = capacity
    };

    private static SessionUpdateRequest NewUpdateRequest(DateTime startsAtUtc, int capacity) => new()
    {
        StartsAtUtc = startsAtUtc,
        DurationMinutes = 180,
        Location = "מטבח הלימוד",
        Capacity = capacity,
        RowVersion = ClientRowVersion
    };
}
