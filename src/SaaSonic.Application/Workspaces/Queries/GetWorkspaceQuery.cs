using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Workspaces.Commands;

namespace SaaSonic.Application.Workspaces.Queries;

public sealed record GetWorkspaceQuery(Guid UserId, Guid WorkspaceId) : IRequest<WorkspaceDto>;

public sealed class GetWorkspaceQueryHandler : IRequestHandler<GetWorkspaceQuery, WorkspaceDto>
{
    private readonly IAppDbContext _db;

    public GetWorkspaceQueryHandler(IAppDbContext db) => _db = db;

    public async Task<WorkspaceDto> Handle(GetWorkspaceQuery request, CancellationToken cancellationToken)
    {
        var membership = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(m => m.Workspace)
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.UserId, cancellationToken);

        if (membership is null)
            throw new ForbiddenException("Workspace not found or you are not a member.");

        var w = membership.Workspace;
        return new WorkspaceDto(w.Id, w.Name, w.Slug, w.OwnerUserId, membership.Role.Name, w.CreatedAt);
    }
}
