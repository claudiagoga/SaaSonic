using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;

namespace SaaSonic.Application.Users.Queries;

public sealed record GetMyProfileQuery(Guid UserId) : IRequest<UserProfileDto>;

public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool EmailVerified,
    DateTimeOffset CreatedAt);

public sealed class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, UserProfileDto>
{
    private readonly IAppDbContext _db;

    public GetMyProfileQueryHandler(IAppDbContext db) => _db = db;

    public async Task<UserProfileDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);

        if (user is null)
            throw new UnauthorizedException("User not found.");

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.EmailVerified,
            user.CreatedAt);
    }
}
