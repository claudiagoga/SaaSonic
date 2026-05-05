using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;
using SaaSonic.Domain.Constants;

namespace SaaSonic.Application.Workspaces.Commands;

public sealed record UpdateWorkspaceCommand(
    Guid UserId,
    Guid WorkspaceId,
    string? Name,
    string? Slug) : IRequest<WorkspaceDto>;

public sealed class UpdateWorkspaceCommandValidator : AbstractValidator<UpdateWorkspaceCommand>
{
    public UpdateWorkspaceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.WorkspaceId).NotEmpty();

        RuleFor(x => x)
            .Must(x => x.Name is not null || x.Slug is not null)
            .WithMessage("At least one field must be provided.");

        When(x => x.Name is not null, () =>
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name cannot be empty.")
                .MaximumLength(200));

        When(x => x.Slug is not null, () =>
            RuleFor(x => x.Slug)
                .NotEmpty().WithMessage("Slug cannot be empty.")
                .MaximumLength(100)
                .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
                .WithMessage("Slug must be lowercase letters, numbers, and hyphens only."));
    }
}

public sealed class UpdateWorkspaceCommandHandler : IRequestHandler<UpdateWorkspaceCommand, WorkspaceDto>
{
    private readonly IAppDbContext _db;

    public UpdateWorkspaceCommandHandler(IAppDbContext db) => _db = db;

    public async Task<WorkspaceDto> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var membership = await _db.WorkspaceMembers
            .AsTracking()
            .Include(m => m.Workspace)
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m =>
                m.WorkspaceId == request.WorkspaceId &&
                m.UserId == request.UserId, cancellationToken);

        if (membership is null)
            throw new ForbiddenException("You are not a member of this workspace.");

        if (membership.Role.Name != RoleNames.Owner && membership.Role.Name != RoleNames.Admin)
            throw new ForbiddenException("Only workspace owners and admins can update workspace settings.");

        var workspace = membership.Workspace;

        if (request.Slug is not null && request.Slug != workspace.Slug)
        {
            if (await _db.Workspaces.AnyAsync(w => w.Slug == request.Slug && w.Id != workspace.Id, cancellationToken))
                throw new ValidationException("A workspace with this slug already exists.");

            workspace.Slug = request.Slug;
        }

        if (request.Name is not null)
            workspace.Name = request.Name;

        workspace.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new WorkspaceDto(workspace.Id, workspace.Name, workspace.Slug, workspace.OwnerUserId, membership.Role.Name, workspace.CreatedAt);
    }
}
