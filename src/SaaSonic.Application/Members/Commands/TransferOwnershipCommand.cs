using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Enums;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;

namespace SaaSonic.Application.Members.Commands;

public sealed record TransferOwnershipCommand(
    Guid WorkspaceId,
    Guid RequestingUserId,
    Guid NewOwnerUserId) : IRequest;

public sealed class TransferOwnershipCommandHandler : IRequestHandler<TransferOwnershipCommand>
{
    private readonly IAppDbContext _db;

    public TransferOwnershipCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(TransferOwnershipCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _db.Workspaces
            .AsTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId && w.DeletedAt == null, cancellationToken);

        if (workspace is null)
            throw new ForbiddenException("Workspace not found.");

        if (workspace.OwnerUserId != request.RequestingUserId)
            throw new ForbiddenException("Only the workspace owner can transfer ownership.");

        if (request.NewOwnerUserId == request.RequestingUserId)
            throw new ValidationException("You are already the owner of this workspace.");

        var newOwnerMembership = await _db.WorkspaceMembers
            .AsTracking()
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.NewOwnerUserId &&
                m.MembershipStatus == MembershipStatus.Active, cancellationToken);

        if (newOwnerMembership is null)
            throw new ForbiddenException("The target user is not an active member of this workspace.");

        var currentOwnerMembership = await _db.WorkspaceMembers
            .AsTracking()
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.RequestingUserId, cancellationToken);

    if (currentOwnerMembership is null)
         throw new InvalidOperationException(
                $"Owner user {request.RequestingUserId} has no member record in workspace {request.WorkspaceId}. Data integrity violation.");

        workspace.OwnerUserId = request.NewOwnerUserId;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;

        newOwnerMembership.RoleId = RoleIds.Owner;
        currentOwnerMembership.RoleId = RoleIds.Admin;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
