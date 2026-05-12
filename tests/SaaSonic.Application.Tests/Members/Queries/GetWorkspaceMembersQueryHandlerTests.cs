using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Members.Queries;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Members.Queries;

public class GetWorkspaceMembersQueryHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly GetWorkspaceMembersQueryHandler _handler;

    public GetWorkspaceMembersQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new GetWorkspaceMembersQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(User owner, Workspace workspace)> SeedWorkspaceAsync()
    {
        if (!_db.Roles.Any(r => r.Id == RoleIds.Owner))
            _db.Roles.Add(new Role { Id = RoleIds.Owner, Name = RoleNames.Owner, Scope = RoleScope.Workspace });

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
            Name = "Test Workspace",
            Slug = "test-workspace",
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

    private async Task<User> AddMemberAsync(Workspace workspace, Guid roleId, string roleName, string email)
    {
        if (!_db.Roles.Any(r => r.Id == roleId))
            _db.Roles.Add(new Role { Id = roleId, Name = roleName, Scope = RoleScope.Workspace });

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
    public async Task Handle_Member_ReturnsMemberList()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        await AddMemberAsync(workspace, RoleIds.Editor, RoleNames.Editor, "editor@test.com");

        var result = await _handler.Handle(
            new GetWorkspaceMembersQuery(workspace.Id, owner.Id), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ViewerCanSeeMemberList()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var viewer = await AddMemberAsync(workspace, RoleIds.Viewer, RoleNames.Viewer, "viewer@test.com");

        var result = await _handler.Handle(
            new GetWorkspaceMembersQuery(workspace.Id, viewer.Id), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyActiveMembers()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = Guid.NewGuid(),
            RoleId = RoleIds.Editor,
            MembershipStatus = MembershipStatus.Revoked,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetWorkspaceMembersQuery(workspace.Id, owner.Id), CancellationToken.None);

        result.Should().HaveCount(1);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonMember_ThrowsForbiddenException()
    {
        var (_, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new GetWorkspaceMembersQuery(workspace.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
