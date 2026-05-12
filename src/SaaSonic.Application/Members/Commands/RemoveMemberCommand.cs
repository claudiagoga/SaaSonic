using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Members.Commands;

public sealed record RemoveMemberCommand(
    Guid WorkspaceId,
    Guid RequestingUserId,
    Guid TargetUserId) : IRequest;

public sealed class RemoveMemberCommandHandler : IRequestHandler<RemoveMemberCommand>
{
    private readonly IAppDbContext _db;

    public RemoveMemberCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var target = await _db.WorkspaceMembers
            .AsTracking()
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.TargetUserId &&
                m.MembershipStatus == MembershipStatus.Active, cancellationToken);

        if (target is null)
            throw new ForbiddenException("Member not found in this workspace.");

        if (target.RoleId == RoleIds.Owner)
            throw new ForbiddenException("The workspace owner cannot be removed. Transfer ownership first.");

        var isSelfRemoval = request.RequestingUserId == request.TargetUserId;

        if (!isSelfRemoval)
        {
            var requester = await _db.WorkspaceMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.WorkspaceId == request.WorkspaceId &&
                    m.UserId == request.RequestingUserId &&
                    m.MembershipStatus == MembershipStatus.Active, cancellationToken);

            if (requester is null)
                throw new ForbiddenException("You are not a member of this workspace.");

            if (requester.RoleId != RoleIds.Owner && requester.RoleId != RoleIds.Admin)
                throw new ForbiddenException("Only owners and admins can remove members.");

            if (requester.RoleId == RoleIds.Admin && target.RoleId == RoleIds.Admin)
                throw new ForbiddenException("Admins cannot remove other admins.");
        }

        _db.WorkspaceMembers.Remove(target);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
