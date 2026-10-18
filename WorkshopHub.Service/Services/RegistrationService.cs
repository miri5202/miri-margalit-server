using AutoMapper;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Registrations;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

/// <summary>
/// הלב של המערכת: הרשמה למפגש וביטול הרשמה – הפעולות על המשאב המוגבל (מקום במפגש).
///
/// הבעיה: בין הרגע שקראנו את המפגש ("נשאר מקום אחד") לבין הרגע ששמרנו ("תפסתי אותו"),
/// בקשה אחרת יכלה לקרוא את אותו מצב ולתפוס את אותו מקום.
///
/// הפתרון (optimistic concurrency): לא נועלים כלום. לשורת המפגש יש RowVersion שמשתנה בכל עדכון,
/// ו-EF Core מוסיף את הערך שנקרא לתנאי ה-WHERE של ה-UPDATE. אם מישהי שינתה את המפגש בינתיים,
/// ה-UPDATE לא מעדכן אף שורה, כל השמירה מתבטלת, ואנחנו מחזירים 409.
///
/// כדי שזה יכסה את כל המקרים, כל פעולה כאן משנה את שורת המפגש עצמה (SeatsTaken או WaitlistCount) –
/// גם הצטרפות לרשימת ההמתנה. כך כל שתי פעולות מקבילות על אותו מפגש מתנגשות, ואחת מהן נדחית.
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly ISessionRepository _sessions;
    private readonly IRegistrationRepository _registrations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly TimeProvider _clock;
    private readonly ILogger<RegistrationService> _logger;

    public RegistrationService(
        ISessionRepository sessions,
        IRegistrationRepository registrations,
        IUnitOfWork unitOfWork,
        IMapper mapper,
        TimeProvider clock,
        ILogger<RegistrationService> logger)
    {
        _sessions = sessions;
        _registrations = registrations;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<RegistrationResponse>> RegisterAsync(
        int sessionId,
        RegistrationCreateRequest request,
        CurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        // 1. קריאה: ה-DbContext עוקב אחרי המפגש וזוכר את ה-RowVersion שלו
        var session = await _sessions.GetForUpdateAsync(sessionId, cancellationToken);
        if (session is null)
            return Errors.Sessions.NotFound(sessionId);

        var now = _clock.GetUtcNow().UtcDateTime;

        // 2. בדיקות עסקיות על המצב שנקרא
        if (session.StartsAtUtc <= now)
            return Errors.Sessions.AlreadyStarted();

        if (await _registrations.HasActiveAsync(sessionId, currentUser.Id, cancellationToken))
            return Errors.Registrations.AlreadyRegistered();

        _logger.LogDebug(
            "Session {SessionId} has {SeatsTaken}/{Capacity} seats taken and {WaitlistCount} waiting before user {UserId} registers",
            sessionId, session.SeatsTaken, session.Capacity, session.WaitlistCount, currentUser.Id);

        var registration = new Registration
        {
            SessionId = session.Id,
            Session = session,
            UserId = currentUser.Id,
            CreatedAtUtc = now
        };

        // 3. שינוי: תופסים מקום, או מצטרפים לרשימת ההמתנה. בשני המקרים שורת המפגש משתנה.
        if (!session.IsFull)
        {
            session.SeatsTaken++;
            registration.Status = RegistrationStatus.Confirmed;
        }
        else if (request.JoinWaitlistIfFull)
        {
            session.WaitlistCount++;
            registration.Status = RegistrationStatus.Waitlisted;
        }
        else
        {
            return Errors.Sessions.Full();
        }

        await _registrations.AddAsync(registration, cancellationToken);

        // 4. שמירה: ה-UPDATE של המפגש (WHERE Id = @id AND RowVersion = @הערך_שנקרא) וה-INSERT של ההרשמה
        //    רצים בטרנזקציה אחת. הבדיקה האמיתית של "האם המקום עדיין פנוי" קורית כאן, בתוך הטרנזקציה.
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // בקשה אחרת שינתה את המפגש בין שלב 1 לשלב 4. שום דבר לא נשמר.
            // זה חריג שטופל (ולא כשל), ולכן Warning – ואלה בדיוק השורות שרוצים למצוא אחר כך בלוג.
            _logger.LogWarning(
                "Concurrency conflict: user {UserId} lost the race for session {SessionId} - the session was changed by another request between read and save",
                currentUser.Id, sessionId);
            return Errors.Sessions.SeatTakenConcurrently();
        }
        catch (UniqueConstraintException)
        {
            // אותה משתמשת שלחה שתי בקשות במקביל, והאינדקס הייחודי בבסיס הנתונים עצר את השנייה
            _logger.LogWarning(
                "Duplicate registration blocked by the unique index: user {UserId}, session {SessionId}",
                currentUser.Id, sessionId);
            return Errors.Registrations.AlreadyRegistered();
        }

        _logger.LogInformation(
            "User {UserId} registered to session {SessionId} as {Status} (registration {RegistrationId}, {SeatsTaken}/{Capacity} seats taken)",
            currentUser.Id, sessionId, registration.Status, registration.Id, session.SeatsTaken, session.Capacity);

        var saved = await _registrations.GetDetailsAsync(registration.Id, cancellationToken) ?? registration;
        return _mapper.Map<RegistrationResponse>(saved);
    }

    public async Task<Result> CancelAsync(int registrationId, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var registration = await _registrations.GetForUpdateAsync(registrationId, cancellationToken);
        if (registration is null)
            return Errors.Registrations.NotFound(registrationId);

        // הרשאה ברמת המשאב: רק בעלת ההרשמה או מנהלת. נבדק כאן, בשרת – לא רק בלקוח.
        if (registration.UserId != currentUser.Id && !currentUser.IsAdmin)
        {
            _logger.LogWarning(
                "User {UserId} tried to cancel registration {RegistrationId} that belongs to another user",
                currentUser.Id, registrationId);
            return Errors.General.Forbidden();
        }

        if (registration.Status == RegistrationStatus.Cancelled)
            return Errors.Registrations.AlreadyCancelled();

        var session = registration.Session;
        var now = _clock.GetUtcNow().UtcDateTime;

        if (session.StartsAtUtc <= now)
            return Errors.Sessions.AlreadyStarted();

        var promotedRegistrationId = (int?)null;

        if (registration.Status == RegistrationStatus.Confirmed)
        {
            // המקום מתפנה...
            session.SeatsTaken--;

            // ...ואם יש ממתינות, הוותיקה ביותר מקבלת אותו מיד, באותה שמירה
            var next = (await _registrations.GetNextWaitlistedAsync(session.Id, 1, cancellationToken)).FirstOrDefault();
            if (next is not null)
            {
                next.Status = RegistrationStatus.Confirmed;
                next.PromotedAtUtc = now;
                session.SeatsTaken++;
                session.WaitlistCount--;
                promotedRegistrationId = next.Id;
            }
        }
        else
        {
            // ביטול של המתנה
            session.WaitlistCount--;
        }

        registration.Status = RegistrationStatus.Cancelled;
        registration.CancelledAtUtc = now;

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning(
                "Concurrency conflict: session {SessionId} was changed by another request while registration {RegistrationId} was being cancelled",
                session.Id, registrationId);
            return Errors.Registrations.ChangedConcurrently();
        }

        _logger.LogInformation(
            "Registration {RegistrationId} for session {SessionId} cancelled by user {UserId}; promoted from waitlist: {PromotedRegistrationId}",
            registrationId, session.Id, currentUser.Id, promotedRegistrationId);

        return Result.Success();
    }

    public async Task<Result<RegistrationResponse>> GetByIdAsync(int registrationId, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        var registration = await _registrations.GetDetailsAsync(registrationId, cancellationToken);
        if (registration is null)
            return Errors.Registrations.NotFound(registrationId);

        if (registration.UserId != currentUser.Id && !currentUser.IsAdmin)
            return Errors.General.Forbidden();

        return _mapper.Map<RegistrationResponse>(registration);
    }

    public async Task<PagedResult<RegistrationResponse>> GetMineAsync(CurrentUser currentUser, RegistrationQuery query, CancellationToken cancellationToken = default)
    {
        var page = await _registrations.GetByUserAsync(currentUser.Id, query, cancellationToken);
        return ToResponsePage(page);
    }

    public async Task<Result<PagedResult<RegistrationResponse>>> GetBySessionAsync(int sessionId, RegistrationQuery query, CancellationToken cancellationToken = default)
    {
        if (await _sessions.GetDetailsAsync(sessionId, cancellationToken) is null)
            return Errors.Sessions.NotFound(sessionId);

        var page = await _registrations.GetBySessionAsync(sessionId, query, cancellationToken);
        return ToResponsePage(page);
    }

    private PagedResult<RegistrationResponse> ToResponsePage(PagedResult<Registration> page) =>
        new(_mapper.Map<List<RegistrationResponse>>(page.Items), page.Page, page.PageSize, page.TotalCount);
}
