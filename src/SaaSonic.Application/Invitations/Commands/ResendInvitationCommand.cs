using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Constants;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Helpers;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Invitations.Commands;

public sealed record ResendInvitationCommand(
    Guid WorkspaceId,
    Guid RequestingUserId,
    Guid InvitationId) : IRequest<InvitationDto>;

public sealed class ResendInvitationCommandHandler : IRequestHandler<ResendInvitationCommand, InvitationDto>
{
    private readonly IAppDbContext _db;
    private readonly IEmailQueue _emailQueue;

    public ResendInvitationCommandHandler(IAppDbContext db, IEmailQueue emailQueue)
    {
        _db = db;
        _emailQueue = emailQueue;
    }

    public async Task<InvitationDto> Handle(ResendInvitationCommand request, CancellationToken cancellationToken)
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
            throw new ForbiddenException("Only owners and admins can resend invitations.");

        var invitation = await _db.WorkspaceInvitations
            .AsTracking()
            .Include(i => i.Role)
            .Include(i => i.Workspace)
            .FirstOrDefaultAsync(i =>
                i.Id == request.InvitationId &&
                i.WorkspaceId == request.WorkspaceId, cancellationToken);

        if (invitation is null)
            throw new ForbiddenException("Invitation not found.");

        if (invitation.Status != WorkspaceInvitationStatus.Pending)
            throw new ForbiddenException("Only pending invitations can be resent.");

        var inviter = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.RequestingUserId, cancellationToken);

        var rawToken = TokenHelper.GenerateSecure();
        invitation.TokenHash = TokenHelper.Hash(rawToken);
        invitation.ExpiresAt = DateTimeOffset.UtcNow.AddDays(7);

        _emailQueue.Enqueue(
            invitation.Email,
            EmailTemplateSlug.WorkspaceInvitation,
            new Dictionary<string, string>
            {
                [EmailTemplatePlaceholder.InviterName] = inviter?.DisplayName ?? "A teammate",
                [EmailTemplatePlaceholder.WorkspaceName] = invitation.Workspace.Name,
                [EmailTemplatePlaceholder.Token] = rawToken,
            });

        await _db.SaveChangesAsync(cancellationToken);

        return new InvitationDto(invitation.Id, invitation.Email, invitation.Role.Name, invitation.ExpiresAt, invitation.CreatedAt);
    }
}
