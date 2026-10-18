using AutoMapper;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Sessions;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

public class SessionService : ISessionService
{
    private readonly ISessionRepository _sessions;
    private readonly IWorkshopRepository _workshops;
    private readonly IRegistrationRepository _registrations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly TimeProvider _clock;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        ISessionRepository sessions,
        IWorkshopRepository workshops,
        IRegistrationRepository registrations,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        TimeProvider clock,
        ILogger<SessionService> logger)
    {
        _sessions = sessions;
        _workshops = workshops;
        _registrations = registrations;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _clock = clock;
        _logger = logger;
    }

    public async Task<PagedResult<SessionResponse>> SearchAsync(SessionQuery query, CurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        var page = await _sessions.SearchAsync(query, UtcNow(), cancellationToken);
        var items = _mapper.Map<List<SessionResponse>>(page.Items);

        await AttachMyRegistrationsAsync(items, currentUser, cancellationToken);

        return new PagedResult<SessionResponse>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<Result<SessionResponse>> GetByIdAsync(int id, CurrentUser? currentUser, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetDetailsAsync(id, cancellationToken);
        if (session is null)
            return Errors.Sessions.NotFound(id);

        var response = _mapper.Map<SessionResponse>(session);
        await AttachMyRegistrationsAsync(new[] { response }, currentUser, cancellationToken);

        return response;
    }

    public async Task<Result<SessionResponse>> CreateAsync(int workshopId, SessionCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _workshops.ExistsAsync(workshopId, cancellationToken))
            return Errors.Workshops.NotFound(workshopId);

        var session = _mapper.Map<Session>(request);
        session.WorkshopId = workshopId;
        session.StartsAtUtc = AsUtc(session.StartsAtUtc);
        session.Location = session.Location.Trim();

        // ולידציה עסקית: תלויה בשעון, ולכן היא כאן ולא ב-Data Annotation על ה-DTO
        if (session.StartsAtUtc <= UtcNow())
            return Errors.Sessions.MustBeInFuture();

        await _sessions.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Session {SessionId} created for workshop {WorkshopId} with capacity {Capacity}",
            session.Id, workshopId, session.Capacity);

        return await LoadResponseAsync(session.Id, cancellationToken);
    }

    public async Task<Result<SessionResponse>> UpdateAsync(int id, SessionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetForUpdateAsync(id, cancellationToken);
        if (session is null)
            return Errors.Sessions.NotFound(id);

        // הלקוח מחזיר את ה-RowVersion שקיבל כשטען את טופס העריכה. קובעים אותו כערך שייבדק בשמירה:
        // אם מישהי נרשמה (או שמנהלת אחרת ערכה) מאז שהטופס נטען – ה-UPDATE לא ימצא שורה, ונחזיר 409
        // במקום לדרוס בשקט את השינוי שלה.
        _sessions.SetOriginalRowVersion(session, request.RowVersion);

        var now = UtcNow();
        var newStartsAtUtc = AsUtc(request.StartsAtUtc!.Value);

        if (newStartsAtUtc != session.StartsAtUtc && newStartsAtUtc <= now)
            return Errors.Sessions.MustBeInFuture();

        if (request.Capacity < session.SeatsTaken)
            return Errors.Sessions.CapacityBelowSeatsTaken(session.SeatsTaken);

        _mapper.Map(request, session);
        session.StartsAtUtc = newStartsAtUtc;
        session.Location = session.Location.Trim();

        // אם נוספו מקומות ויש ממתינות – מקדמים אותן לפי סדר ההצטרפות, באותה שמירה
        var promoted = await PromoteFromWaitlistAsync(session, now, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Concurrency conflict: session {SessionId} was modified by another request while it was being edited",
                id);
            return Errors.Sessions.ModifiedConcurrently();
        }

        _logger.LogInformation(
            "Session {SessionId} updated (capacity {Capacity}, {Promoted} promoted from the waitlist)",
            id, session.Capacity, promoted);

        return await LoadResponseAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var session = await _sessions.GetForUpdateAsync(id, cancellationToken);
        if (session is null)
            return Errors.Sessions.NotFound(id);

        if (session.SeatsTaken > 0 || session.WaitlistCount > 0)
            return Errors.Sessions.HasActiveRegistrations();

        _sessions.Remove(session);

        try
        {
            // גם ה-DELETE כולל את ה-RowVersion ב-WHERE: אם מישהי נרשמה בין הבדיקה למחיקה, המחיקה תיכשל
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Concurrency conflict: session {SessionId} was modified by another request while it was being deleted",
                id);
            return Errors.Sessions.ModifiedConcurrently();
        }

        _logger.LogInformation("Session {SessionId} deleted", id);
        return Result.Success();
    }

    private async Task<int> PromoteFromWaitlistAsync(Session session, DateTime now, CancellationToken cancellationToken)
    {
        var freeSeats = session.Capacity - session.SeatsTaken;
        if (freeSeats <= 0 || session.WaitlistCount <= 0)
            return 0;

        var toPromote = await _registrations.GetNextWaitlistedAsync(
            session.Id, Math.Min(freeSeats, session.WaitlistCount), cancellationToken);

        foreach (var registration in toPromote)
        {
            registration.Status = RegistrationStatus.Confirmed;
            registration.PromotedAtUtc = now;
            session.SeatsTaken++;
            session.WaitlistCount--;
        }

        return toPromote.Count;
    }

    /// <summary>
    /// מסמן בכל מפגש את ההרשמה הפעילה של המשתמשת המחוברת.
    /// שאילתה אחת לכל המפגשים שבעמוד – ולא שאילתה לכל מפגש (N+1).
    /// </summary>
    private async Task AttachMyRegistrationsAsync(IReadOnlyCollection<SessionResponse> sessions, CurrentUser? currentUser, CancellationToken cancellationToken)
    {
        if (currentUser is null || sessions.Count == 0)
            return;

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var mine = await _registrations.GetActiveByUserForSessionsAsync(currentUser.Id, sessionIds, cancellationToken);

        var bySessionId = new Dictionary<int, Registration>();
        foreach (var registration in mine)
            bySessionId[registration.SessionId] = registration;

        foreach (var session in sessions)
        {
            if (bySessionId.TryGetValue(session.Id, out var registration))
            {
                session.MyRegistrationId = registration.Id;
                session.MyRegistrationStatus = registration.Status;
            }
        }
    }

    private async Task<Result<SessionResponse>> LoadResponseAsync(int id, CancellationToken cancellationToken)
    {
        var saved = await _sessions.GetDetailsAsync(id, cancellationToken);
        if (saved is null)
            return Errors.Sessions.NotFound(id);

        return _mapper.Map<SessionResponse>(saved);
    }

    private DateTime UtcNow() => _clock.GetUtcNow().UtcDateTime;

    /// <summary>כל התאריכים במערכת ב-UTC. תאריך שהגיע בלי אזור זמן מתפרש כ-UTC.</summary>
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
