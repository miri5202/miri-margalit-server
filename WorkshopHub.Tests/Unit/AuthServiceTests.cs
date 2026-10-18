using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.Entities;
using WorkshopHub.Core.Enums;
using WorkshopHub.Core.Exceptions;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;
using WorkshopHub.Service.Services;
using WorkshopHub.Tests.Helpers;

namespace WorkshopHub.Tests.Unit;

public class AuthServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<ITokenService> _tokenService = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("HASHED");

        _service = new AuthService(
            _users.Object,
            _unitOfWork.Object,
            _passwordHasher.Object,
            _tokenService.Object,
            TestMapper.Instance,
            new FixedTimeProvider(Now),
            NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_CreatesMemberWithHashedPassword()
    {
        User? added = null;
        _users
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => added = user)
            .Returns(Task.CompletedTask);

        var request = new RegisterUserRequest { FullName = "  רחל כהן ", Email = "  Rachel@Example.COM ", Password = "Secret#123" };

        var result = await _service.RegisterAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);

        // המייל מנורמל, השם מנוקה מרווחים
        Assert.Equal("rachel@example.com", added!.Email);
        Assert.Equal("רחל כהן", added.FullName);

        // נשמר רק הגיבוב – הסיסמה עצמה לא נשמרת
        Assert.Equal("HASHED", added.PasswordHash);
        Assert.NotEqual(request.Password, added.PasswordHash);

        // התפקיד נקבע בשרת: משתמשת חדשה היא תמיד Member
        Assert.Equal(UserRole.Member, added.Role);
        Assert.Equal(UserRole.Member, result.Value.Role);
        Assert.Equal(Now, added.CreatedAtUtc);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailIsTaken_ReturnsConflictAndSavesNothing()
    {
        _users.Setup(r => r.EmailExistsAsync("rachel@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.RegisterAsync(
            new RegisterUserRequest { FullName = "רחל כהן", Email = "rachel@example.com", Password = "Secret#123" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(ErrorCodes.EmailTaken, result.Error.Code);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenTwoSignUpsRaceOnTheSameEmail_TheUniqueIndexTurnsTheLoserIntoConflict()
    {
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueConstraintException("duplicate e-mail"));

        var result = await _service.RegisterAsync(
            new RegisterUserRequest { FullName = "רחל כהן", Email = "rachel@example.com", Password = "Secret#123" },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.EmailTaken, result.Error!.Code);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndUser()
    {
        var user = GivenUser();
        var expiresAtUtc = Now.AddHours(1);
        _passwordHasher.Setup(h => h.Verify("Secret#123", user.PasswordHash)).Returns(true);
        _tokenService.Setup(t => t.CreateToken(user)).Returns(new AccessToken("signed.jwt.token", expiresAtUtc));

        var result = await _service.LoginAsync(
            new LoginRequest { Email = "RACHEL@example.com", Password = "Secret#123" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("signed.jwt.token", result.Value.AccessToken);
        Assert.Equal(expiresAtUtc, result.Value.ExpiresAtUtc);
        Assert.Equal(user.Id, result.Value.User.Id);
        Assert.Equal(UserRole.Member, result.Value.User.Role);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsUnauthorizedAndIssuesNoToken()
    {
        var user = GivenUser();
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), user.PasswordHash)).Returns(false);

        var result = await _service.LoginAsync(
            new LoginRequest { Email = "rachel@example.com", Password = "wrong" }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal(ErrorCodes.InvalidCredentials, result.Error.Code);
        _tokenService.Verify(t => t.CreateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ReturnsTheSameErrorAsWrongPassword()
    {
        // אותה תשובה בדיוק בשני המקרים, כדי לא לחשוף אילו כתובות מייל רשומות במערכת
        var result = await _service.LoginAsync(
            new LoginRequest { Email = "nobody@example.com", Password = "Secret#123" }, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal(ErrorCodes.InvalidCredentials, result.Error.Code);
    }

    private User GivenUser()
    {
        var user = new User
        {
            Id = 7,
            FullName = "רחל כהן",
            Email = "rachel@example.com",
            PasswordHash = "STORED-HASH",
            Role = UserRole.Member,
            CreatedAtUtc = Now.AddDays(-30)
        };

        _users.Setup(r => r.GetByEmailAsync("rachel@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(user);
        return user;
    }
}
