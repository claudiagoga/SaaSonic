using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Enums;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;

namespace SaaSonic.Application.Workspaces.Commands;

public sealed record DeleteWorkspaceCommand(Guid UserId, Guid WorkspaceId) : IRequest;

public sealed class DeleteWorkspaceCommandHandler : IRequestHandler<DeleteWorkspaceCommand>
{
    private readonly IAppDbContext _db;

    public DeleteWorkspaceCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DeleteWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _db.Workspaces
            .AsTracking()
            .Include(w => w.Subscription)
            .Include(w => w.Invitations)
            .FirstOrDefaultAsync(w => w.Id == request.WorkspaceId, cancellationToken);

        if (workspace is null)
            throw new ForbiddenException("Workspace not found.");

        if (workspace.OwnerUserId != request.UserId)
            throw new ForbiddenException("Only the workspace owner can delete the workspace.");

        if (workspace.Subscription is not null &&
            workspace.Subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Trialing)
            throw new ValidationException("Cancel your subscription before deleting the workspace.");

        // Cancel all pending invitations
        foreach (var invitation in workspace.Invitations)
            if (invitation.Status == WorkspaceInvitationStatus.Pending)
                invitation.Status = WorkspaceInvitationStatus.Revoked;

        // Mangle slug to free it for reuse
        workspace.Slug = $"{workspace.Slug}-deleted-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        workspace.DeletedAt = DateTimeOffset.UtcNow;
        workspace.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
