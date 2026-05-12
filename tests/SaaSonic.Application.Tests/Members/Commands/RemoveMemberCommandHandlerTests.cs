using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Members.Commands;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Members.Commands;

public class RemoveMemberCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly RemoveMemberCommandHandler _handler;

    public RemoveMemberCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new RemoveMemberCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(User owner, Workspace workspace)> SeedWorkspaceAsync()
    {
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
    public async Task Handle_OwnerRemovesEditor_DeletesMembership()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor);

        await _handler.Handle(new RemoveMemberCommand(workspace.Id, owner.Id, editor.Id), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.UserId == editor.Id && m.WorkspaceId == workspace.Id);
        membership.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AdminRemovesEditor_DeletesMembership()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin = await AddMemberAsync(workspace, RoleIds.Admin, "admin@test.com");
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");

        await _handler.Handle(new RemoveMemberCommand(workspace.Id, admin.Id, editor.Id), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.UserId == editor.Id && m.WorkspaceId == workspace.Id);
        membership.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MemberLeavesSelf_DeletesMembership()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor);

        await _handler.Handle(new RemoveMemberCommand(workspace.Id, editor.Id, editor.Id), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.UserId == editor.Id && m.WorkspaceId == workspace.Id);
        membership.Should().BeNull();
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerTriesToLeave_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new RemoveMemberCommand(workspace.Id, owner.Id, owner.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AdminRemovesAdmin_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin1 = await AddMemberAsync(workspace, RoleIds.Admin, "admin1@test.com");
        var admin2 = await AddMemberAsync(workspace, RoleIds.Admin, "admin2@test.com");

        var act = () => _handler.Handle(
            new RemoveMemberCommand(workspace.Id, admin1.Id, admin2.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_EditorRemovesAnotherMember_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");
        var viewer = await AddMemberAsync(workspace, RoleIds.Viewer, "viewer@test.com");

        var act = () => _handler.Handle(
            new RemoveMemberCommand(workspace.Id, editor.Id, viewer.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_TargetNotMember_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new RemoveMemberCommand(workspace.Id, owner.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
