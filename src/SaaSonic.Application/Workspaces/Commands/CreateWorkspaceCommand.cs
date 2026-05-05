using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Workspaces.Commands;

public sealed record CreateWorkspaceCommand(
    Guid UserId,
    string Name,
    string Slug) : IRequest<WorkspaceDto>;

public sealed record WorkspaceDto(
    Guid Id,
    string Name,
    string Slug,
    Guid OwnerUserId,
    string UserRole,
    DateTimeOffset CreatedAt);

public sealed class CreateWorkspaceCommandValidator : AbstractValidator<CreateWorkspaceCommand>
{
    public CreateWorkspaceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Workspace name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Slug is required.")
            .MaximumLength(100)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only (e.g. my-workspace).");
    }
}

public sealed class CreateWorkspaceCommandHandler : IRequestHandler<CreateWorkspaceCommand, WorkspaceDto>
{
    private readonly IAppDbContext _db;

    public CreateWorkspaceCommandHandler(IAppDbContext db) => _db = db;

    public async Task<WorkspaceDto> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);

        if (user is null)
            throw new UnauthorizedException("User not found.");

        if (await _db.Workspaces.AnyAsync(w => w.Slug == request.Slug, cancellationToken))
            throw new ValidationException("A workspace with this slug already exists.");

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug,
            OwnerUserId = request.UserId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var membership = new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = request.UserId,
            RoleId = RoleIds.Owner,
            MembershipStatus = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Workspaces.Add(workspace);
        _db.WorkspaceMembers.Add(membership);
        await _db.SaveChangesAsync(cancellationToken);

        return new WorkspaceDto(workspace.Id, workspace.Name, workspace.Slug, workspace.OwnerUserId, RoleNames.Owner, workspace.CreatedAt);
    }
}
