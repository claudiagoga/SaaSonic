using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Workspaces.Commands;

namespace SaaSonic.Application.Workspaces.Queries;

public sealed record GetMyWorkspacesQuery(Guid UserId) : IRequest<IReadOnlyList<WorkspaceDto>>;

public sealed class GetMyWorkspacesQueryHandler : IRequestHandler<GetMyWorkspacesQuery, IReadOnlyList<WorkspaceDto>>
{
    private readonly IAppDbContext _db;

    public GetMyWorkspacesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WorkspaceDto>> Handle(GetMyWorkspacesQuery request, CancellationToken cancellationToken)
    {
        var memberships = await _db.WorkspaceMembers
            .AsNoTracking()
            .Include(m => m.Workspace)
            .Include(m => m.Role)
            .Where(m => m.UserId == request.UserId)
            .OrderBy(m => m.Workspace.Name)
            .ToListAsync(cancellationToken);

        return memberships
            .Select(m => new WorkspaceDto(
                m.Workspace.Id,
                m.Workspace.Name,
                m.Workspace.Slug,
                m.Workspace.OwnerUserId,
                m.Role.Name,
                m.Workspace.CreatedAt))
            .ToList();
    }
}
