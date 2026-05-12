using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Members.Commands;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Members.Commands;

public class ChangeMemberRoleCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly ChangeMemberRoleCommandHandler _handler;

    public ChangeMemberRoleCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new ChangeMemberRoleCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private void SeedRoles()
    {
        _db.Roles.AddRange(
            new Role { Id = RoleIds.Owner,  Name = RoleNames.Owner,  Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Admin,  Name = RoleNames.Admin,  Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Editor, Name = RoleNames.Editor, Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Viewer, Name = RoleNames.Viewer, Scope = RoleScope.Workspace }
        );
        _db.SaveChanges();
    }

    private async Task<(User user, Workspace workspace)> SeedWorkspaceAsync()
    {
        SeedRoles();
        var owner = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@test.com",
            DisplayName = "Owner",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Slug = "test",
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(owner);
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
        return (owner, workspace);
    }

    private async Task<User> AddMemberAsync(Workspace workspace, Guid roleId, string email = "member@test.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Member",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
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
        return user;
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerChangesEditorToViewer_UpdatesRole()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor);

        var result = await _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, owner.Id, editor.Id, RoleIds.Viewer), CancellationToken.None);

        result.Role.Should().Be(RoleNames.Viewer);
        var membership = await _db.WorkspaceMembers.FirstAsync(m => m.UserId == editor.Id);
        membership.RoleId.Should().Be(RoleIds.Viewer);
    }

    [Fact]
    public async Task Handle_OwnerPromotesEditorToAdmin_UpdatesRole()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor);

        var result = await _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, owner.Id, editor.Id, RoleIds.Admin), CancellationToken.None);

        result.Role.Should().Be(RoleNames.Admin);
    }

    [Fact]
    public async Task Handle_AdminChangesEditorRole_UpdatesRole()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin = await AddMemberAsync(workspace, RoleIds.Admin, "admin@test.com");
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");

        var result = await _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, admin.Id, editor.Id, RoleIds.Viewer), CancellationToken.None);

        result.Role.Should().Be(RoleNames.Viewer);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AdminChangesAnotherAdminRole_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin1 = await AddMemberAsync(workspace, RoleIds.Admin, "admin1@test.com");
        var admin2 = await AddMemberAsync(workspace, RoleIds.Admin, "admin2@test.com");

        var act = () => _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, admin1.Id, admin2.Id, RoleIds.Viewer), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AdminPromotesToAdmin_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin = await AddMemberAsync(workspace, RoleIds.Admin, "admin@test.com");
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");

        var act = () => _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, admin.Id, editor.Id, RoleIds.Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_TargetIsOwner_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin = await AddMemberAsync(workspace, RoleIds.Admin);

        var act = () => _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, admin.Id, owner.Id, RoleIds.Editor), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_EditorAttempts_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");
        var viewer = await AddMemberAsync(workspace, RoleIds.Viewer, "viewer@test.com");

        var act = () => _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, editor.Id, viewer.Id, RoleIds.Editor), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NonMember_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor);

        var act = () => _handler.Handle(
            new ChangeMemberRoleCommand(workspace.Id, Guid.NewGuid(), editor.Id, RoleIds.Viewer), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
