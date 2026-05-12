using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Constants;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Helpers;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;

namespace SaaSonic.Application.Invitations.Commands;

public sealed record InviteMemberCommand(
    Guid WorkspaceId,
    Guid InvitedByUserId,
    string Email,
    Guid RoleId) : IRequest<InvitationDto>;

public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt);

public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.InvitedByUserId).NotEmpty();
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email address is required.");
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class InviteMemberCommandHandler : IRequestHandler<InviteMemberCommand, InvitationDto>
{
    private readonly IAppDbContext _db;
    private readonly IEmailQueue _emailQueue;

    public InviteMemberCommandHandler(IAppDbContext db, IEmailQueue emailQueue)
    {
        _db = db;
        _emailQueue = emailQueue;
    }

    public async Task<InvitationDto> Handle(InviteMemberCommand request, CancellationToken cancellationToken)
    {
        var requester = await _db.WorkspaceMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.InvitedByUserId &&
                m.MembershipStatus == MembershipStatus.Active, cancellationToken);

        if (requester is null)
            throw new ForbiddenException("You are not a member of this workspace.");

        if (requester.RoleId != RoleIds.Owner && requester.RoleId != RoleIds.Admin)
            throw new ForbiddenException("Only owners and admins can invite members.");

        // Admins cannot invite at Owner or Admin level
        if (requester.RoleId == RoleIds.Admin &&
            (request.RoleId == RoleIds.Owner || request.RoleId == RoleIds.Admin))
            throw new ForbiddenException("Admins can only invite members at the Editor or Viewer level.");

        var validWorkspaceRoles = new[] { RoleIds.Admin, RoleIds.Editor, RoleIds.Viewer };
        if (!validWorkspaceRoles.Contains(request.RoleId))
            throw new ValidationException("Invalid role. Must be Admin, Editor, or Viewer.");

        var workspace = await _db.Workspaces
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.DeletedAt == null, cancellationToken);

        if (workspace is null)
            throw new ForbiddenException("Workspace not found.");

        var normalizedEmail = request.Email.ToLowerInvariant();

        // If the invitee already has an account, verify they are not already a member
        var existingUser = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            var alreadyMember = await _db.WorkspaceMembers
                .AnyAsync(m =>
                    m.WorkspaceId == request.WorkspaceId &&
                    m.UserId == existingUser.Id &&
                    m.MembershipStatus == MembershipStatus.Active, cancellationToken);

            if (alreadyMember)
                throw new ValidationException("This user is already a member of the workspace.");
        }

        var pendingExists = await _db.WorkspaceInvitations
            .AnyAsync(i =>
                i.WorkspaceId == request.WorkspaceId &&
                i.Email == normalizedEmail &&
                i.Status == WorkspaceInvitationStatus.Pending, cancellationToken);

        if (pendingExists)
            throw new ValidationException("An invitation for this email is already pending.");

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

        if (role is null)
            throw new ValidationException("Role not found.");

        var inviter = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.InvitedByUserId, cancellationToken);

        var rawToken = TokenHelper.GenerateSecure();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

        var invitation = new WorkspaceInvitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            InvitedByUserId = request.InvitedByUserId,
            Email = normalizedEmail,
            RoleId = request.RoleId,
            Status = WorkspaceInvitationStatus.Pending,
            TokenHash = TokenHelper.Hash(rawToken),
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.WorkspaceInvitations.Add(invitation);

        _emailQueue.Enqueue(
            request.Email,
            EmailTemplateSlug.WorkspaceInvitation,
            new Dictionary<string, string>
            {
                [EmailTemplatePlaceholder.InviterName] = inviter?.DisplayName ?? "A teammate",
                [EmailTemplatePlaceholder.WorkspaceName] = workspace.Name,
                [EmailTemplatePlaceholder.Token] = rawToken,
            });

        await _db.SaveChangesAsync(cancellationToken);

        return new InvitationDto(invitation.Id, normalizedEmail, role.Name, expiresAt, invitation.CreatedAt);
    }
}
