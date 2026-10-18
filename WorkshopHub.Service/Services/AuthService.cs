using AutoMapper;
using Microsoft.Extensions.Logging;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper,
        TimeProvider clock,
        ILogger<AuthService> logger)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<UserResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        // ולידציה עסקית (תלויה במצב הנתונים) – ולכן היא כאן ולא ב-Controller
        if (await _users.EmailExistsAsync(email, cancellationToken))
            return Errors.Auth.EmailTaken();

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            // התפקיד נקבע בשרת. הלקוח לא יכול לבקש להיות Admin.
            Role = UserRole.Member,
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
        };

        await _users.AddAsync(user, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintException)
        {
            // שתי הרשמות עם אותו מייל באותו רגע: שתיהן עברו את הבדיקה למעלה,
            // והאינדקס הייחודי בבסיס הנתונים עצר את השנייה.
            _logger.LogWarning("Registration rejected by the unique e-mail index (two concurrent sign-ups with the same address)");
            return Errors.Auth.EmailTaken();
        }

        // שימי לב: לא כותבים ללוג את המייל, הסיסמה או כל תוכן אחר של בקשת ההרשמה – רק את המזהה שנוצר
        _logger.LogInformation("New user {UserId} registered", user.Id);

        return _mapper.Map<UserResponse>(user);
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByEmailAsync(NormalizeEmail(request.Email), cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt");
            return Errors.Auth.InvalidCredentials();
        }

        var token = _tokenService.CreateToken(user);

        // ה-token עצמו לעולם לא נכתב ללוג
        _logger.LogInformation("User {UserId} logged in as {Role}", user.Id, user.Role);

        return new TokenResponse
        {
            AccessToken = token.Value,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = _mapper.Map<UserResponse>(user)
        };
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
