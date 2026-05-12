using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Helpers;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Workspaces.Commands;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Invitations.Commands;

public sealed record AcceptInvitationCommand(
    Guid UserId,
    string Token) : IRequest<WorkspaceDto>;

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().WithMessage("Invitation token is required.");
    }
}

public sealed class AcceptInvitationCommandHandler : IRequestHandler<AcceptInvitationCommand, WorkspaceDto>
{
    private readonly IAppDbContext _db;

    public AcceptInvitationCommandHandler(IAppDbContext db) => _db = db;

    public async Task<WorkspaceDto> Handle(AcceptInvitationCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHelper.Hash(request.Token);

        var invitation = await _db.WorkspaceInvitations
            .AsTracking()
            .Include(i => i.Workspace)
            .Include(i => i.Role)
            .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || invitation.Status != WorkspaceInvitationStatus.Pending)
            throw new ForbiddenException("Invitation not found or already used.");

        if (invitation.ExpiresAt < DateTimeOffset.UtcNow)
        {
            invitation.Status = WorkspaceInvitationStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            throw new ForbiddenException("This invitation has expired.");
        }

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);

        if (user is null)
            throw new UnauthorizedException("User not found.");

        if (!user.Email.Equals(invitation.Email, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("This invitation was sent to a different email address.");

        var alreadyMember = await _db.WorkspaceMembers
            .AnyAsync(m =>
                m.WorkspaceId == invitation.WorkspaceId &&
                m.UserId == request.UserId &&
                m.MembershipStatus == MembershipStatus.Active, cancellationToken);

        invitation.Status = WorkspaceInvitationStatus.Accepted;

        if (!alreadyMember)
        {
            _db.WorkspaceMembers.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = invitation.WorkspaceId,
                UserId = request.UserId,
                RoleId = invitation.RoleId,
                MembershipStatus = MembershipStatus.Active,
                InvitedByUserId = invitation.InvitedByUserId,
                JoinedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        var w = invitation.Workspace;
        return new WorkspaceDto(w.Id, w.Name, w.Slug, w.OwnerUserId, invitation.Role.Name, w.CreatedAt);
    }
}
