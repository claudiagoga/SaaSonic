using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;

namespace SaaSonic.Application.Users.Queries;

public sealed record GetUserByIdQuery(Guid TargetUserId) : IRequest<UserProfileDto>;

public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserProfileDto>
{
    private readonly IAppDbContext _db;

    public GetUserByIdQueryHandler(IAppDbContext db) => _db = db;

    public async Task<UserProfileDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, cancellationToken);

        if (user is null)
            throw new ValidationException("User not found.");

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.EmailVerified,
            user.CreatedAt);
    }
}
