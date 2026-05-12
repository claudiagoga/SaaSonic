using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Members.Queries;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Enums;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;

namespace SaaSonic.Application.Members.Commands;

public sealed record ChangeMemberRoleCommand(
    Guid WorkspaceId,
    Guid RequestingUserId,
    Guid TargetUserId,
    Guid NewRoleId) : IRequest<MemberDto>;

public sealed class ChangeMemberRoleCommandValidator : AbstractValidator<ChangeMemberRoleCommand>
{
    public ChangeMemberRoleCommandValidator()
    {
        RuleFor(x => x.WorkspaceId).NotEmpty();
        RuleFor(x => x.RequestingUserId).NotEmpty();
        RuleFor(x => x.TargetUserId).NotEmpty();
        RuleFor(x => x.NewRoleId).NotEmpty();
    }
}

public sealed class ChangeMemberRoleCommandHandler : IRequestHandler<ChangeMemberRoleCommand, MemberDto>
{
    private readonly IAppDbContext _db;

    public ChangeMemberRoleCommandHandler(IAppDbContext db) => _db = db;

    public async Task<MemberDto> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
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
            throw new ForbiddenException("Only owners and admins can change member roles.");

        var target = await _db.WorkspaceMembers
            .AsTracking()
            .Include(m => m.User)
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.TargetUserId &&
                m.MembershipStatus == MembershipStatus.Active, cancellationToken);

        if (target is null)
            throw new ForbiddenException("Member not found in this workspace.");

        if (target.RoleId == RoleIds.Owner)
            throw new ForbiddenException("Cannot change the owner's role. Use transfer ownership instead.");

        if (requester.RoleId == RoleIds.Admin)
        {
            if (target.RoleId == RoleIds.Admin)
                throw new ForbiddenException("Admins cannot change another admin's role.");

            if (request.NewRoleId == RoleIds.Owner || request.NewRoleId == RoleIds.Admin)
                throw new ForbiddenException("Admins can only assign the Editor or Viewer role.");
        }

        var validRoles = new[] { RoleIds.Admin, RoleIds.Editor, RoleIds.Viewer };
        if (!validRoles.Contains(request.NewRoleId))
            throw new ValidationException("Invalid role. Must be Admin, Editor, or Viewer.");

        var newRole = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.NewRoleId, cancellationToken);

        if (newRole is null)
            throw new ValidationException("Role not found.");

        target.RoleId = request.NewRoleId;
        await _db.SaveChangesAsync(cancellationToken);

        return new MemberDto(
            target.UserId,
            target.User.DisplayName,
            target.User.AvatarUrl,
            target.User.Email,
            newRole.Name,
            target.JoinedAt ?? target.CreatedAt);
    }
}
