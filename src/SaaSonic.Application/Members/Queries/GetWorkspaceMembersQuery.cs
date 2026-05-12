using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Members.Queries;

public sealed record GetWorkspaceMembersQuery(
    Guid WorkspaceId,
    Guid RequestingUserId) : IRequest<IReadOnlyList<MemberDto>>;

public sealed record MemberDto(
    Guid UserId,
    string DisplayName,
    string? AvatarUrl,
    string Email,
    string Role,
    DateTimeOffset JoinedAt);

public sealed class GetWorkspaceMembersQueryHandler : IRequestHandler<GetWorkspaceMembersQuery, IReadOnlyList<MemberDto>>
{
    private readonly IAppDbContext _db;

    public GetWorkspaceMembersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MemberDto>> Handle(GetWorkspaceMembersQuery request, CancellationToken cancellationToken)
    {
        var isMember = await _db.WorkspaceMembers
            .AsNoTracking()
            .AnyAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.RequestingUserId &&
                m.MembershipStatus == MembershipStatus.Active, cancellationToken);

        if (!isMember)
            throw new ForbiddenException("Workspace not found or you are not a member.");

        return await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Role)
            .Where(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.MembershipStatus == MembershipStatus.Active)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new MemberDto(
                m.UserId,
                m.User.DisplayName,
                m.User.AvatarUrl,
                m.User.Email,
                m.Role.Name,
                m.JoinedAt ?? m.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
