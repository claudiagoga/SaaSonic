using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Workspaces.Commands;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Workspaces.Commands;

public class UpdateWorkspaceCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly UpdateWorkspaceCommandHandler _handler;

    public UpdateWorkspaceCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new UpdateWorkspaceCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(string email = "user@test.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<(Workspace workspace, Role role)> SeedWorkspaceAsync(User owner)
    {
        var ownerRole = new Role { Id = RoleIds.Owner, Name = RoleNames.Owner, Scope = RoleScope.Workspace };
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Original Name",
            Slug = "original-slug",
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Roles.Add(ownerRole);
        _db.Workspaces.Add(workspace);
        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = owner.Id,
            RoleId = RoleIds.Owner,
            MembershipStatus = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return (workspace, ownerRole);
    }

    private async Task AddMemberAsync(Workspace workspace, User user, Guid roleId, string roleName)
    {
        if (!_db.Roles.Any(r => r.Id == roleId))
            _db.Roles.Add(new Role { Id = roleId, Name = roleName, Scope = RoleScope.Workspace });

        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            RoleId = roleId,
            MembershipStatus = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerUpdatesName_UpdatesWorkspace()
    {
        var owner = await SeedUserAsync();
        var (workspace, _) = await SeedWorkspaceAsync(owner);

        var result = await _handler.Handle(
            new UpdateWorkspaceCommand(owner.Id, workspace.Id, "New Name", null), CancellationToken.None);

        result.Name.Should().Be("New Name");
        var updated = await _db.Workspaces.FindAsync(workspace.Id);
        updated!.Name.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_AdminUpdatesName_UpdatesWorkspace()
    {
        var owner = await SeedUserAsync("owner@test.com");
        var admin = await SeedUserAsync("admin@test.com");
        var (workspace, _) = await SeedWorkspaceAsync(owner);
        await AddMemberAsync(workspace, admin, RoleIds.Admin, RoleNames.Admin);

        var result = await _handler.Handle(
            new UpdateWorkspaceCommand(admin.Id, workspace.Id, "Admin Updated", null), CancellationToken.None);

        result.Name.Should().Be("Admin Updated");
    }

    [Fact]
    public async Task Handle_OwnerUpdatesSlug_UpdatesWorkspace()
    {
        var owner = await SeedUserAsync();
        var (workspace, _) = await SeedWorkspaceAsync(owner);

        var result = await _handler.Handle(
            new UpdateWorkspaceCommand(owner.Id, workspace.Id, null, "new-slug"), CancellationToken.None);

        result.Slug.Should().Be("new-slug");
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateSlug_ThrowsValidationException()
    {
        var owner = await SeedUserAsync();
        var (workspace, _) = await SeedWorkspaceAsync(owner);
        _db.Workspaces.Add(new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Other",
            Slug = "taken-slug",
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new UpdateWorkspaceCommand(owner.Id, workspace.Id, null, "taken-slug"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_NonMember_ThrowsForbiddenException()
    {
        var owner = await SeedUserAsync("owner@test.com");
        var stranger = await SeedUserAsync("stranger@test.com");
        var (workspace, _) = await SeedWorkspaceAsync(owner);

        var act = () => _handler.Handle(
            new UpdateWorkspaceCommand(stranger.Id, workspace.Id, "Name", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_ViewerUpdates_ThrowsForbiddenException()
    {
        var owner = await SeedUserAsync("owner@test.com");
        var viewer = await SeedUserAsync("viewer@test.com");
        var (workspace, _) = await SeedWorkspaceAsync(owner);
        await AddMemberAsync(workspace, viewer, RoleIds.Viewer, RoleNames.Viewer);

        var act = () => _handler.Handle(
            new UpdateWorkspaceCommand(viewer.Id, workspace.Id, "Name", null), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
