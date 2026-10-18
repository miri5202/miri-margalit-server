using AutoMapper;
using WorkshopHub.Core.Common;
using WorkshopHub.Core.DTOs.Users;
using WorkshopHub.Core.Interfaces.Repositories;
using WorkshopHub.Core.Interfaces.Services;

namespace WorkshopHub.Service.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _users;
    private readonly IMapper _mapper;

    public UserService(IUserRepository users, IMapper mapper)
    {
        _users = users;
        _mapper = mapper;
    }

    public async Task<Result<UserResponse>> GetByIdAsync(int id, CurrentUser currentUser, CancellationToken cancellationToken = default)
    {
        // הרשאה ברמת המשאב: משתמשת רגילה רואה רק את עצמה
        if (id != currentUser.Id && !currentUser.IsAdmin)
            return Errors.General.Forbidden();

        var user = await _users.GetByIdAsync(id, cancellationToken);
        if (user is null)
            return Errors.Users.NotFound(id);

        return _mapper.Map<UserResponse>(user);
    }
}
