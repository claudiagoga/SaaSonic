using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Workspaces.Queries;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Workspaces.Queries;

public class GetWorkspaceQueryHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly GetWorkspaceQueryHandler _handler;

    public GetWorkspaceQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new GetWorkspaceQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(User user, Workspace workspace)> SeedMemberAsync(
        Guid roleId = default, string roleName = RoleNames.Owner)
    {
        if (roleId == default) roleId = RoleIds.Owner;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        var role = new Role { Id = roleId, Name = roleName, Scope = RoleScope.Workspace };
        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Test Workspace",
            Slug = "test-workspace",
            OwnerUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        _db.Roles.Add(role);
        _db.Workspaces.Add(workspace);
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
        return (user, workspace);
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Member_ReturnsWorkspace()
    {
        var (user, workspace) = await SeedMemberAsync();

        var result = await _handler.Handle(
            new GetWorkspaceQuery(user.Id, workspace.Id), CancellationToken.None);

        result.Id.Should().Be(workspace.Id);
        result.Name.Should().Be(workspace.Name);
        result.UserRole.Should().Be(RoleNames.Owner);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonMember_ThrowsForbiddenException()
    {
        var (_, workspace) = await SeedMemberAsync();

        var act = () => _handler.Handle(
            new GetWorkspaceQuery(Guid.NewGuid(), workspace.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
