using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Workspaces.Queries;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Workspaces.Queries;

public class GetMyWorkspacesQueryHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly GetMyWorkspacesQueryHandler _handler;

    public GetMyWorkspacesQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new GetMyWorkspacesQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "User",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Workspace> SeedMembershipAsync(User user, string name, string slug)
    {
        if (!_db.Roles.Any(r => r.Id == RoleIds.Owner))
            _db.Roles.Add(new Role { Id = RoleIds.Owner, Name = RoleNames.Owner, Scope = RoleScope.Workspace });

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            OwnerUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Workspaces.Add(workspace);
        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            RoleId = RoleIds.Owner,
            MembershipStatus = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return workspace;
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserWithWorkspaces_ReturnsAll()
    {
        var user = await SeedUserAsync();
        await SeedMembershipAsync(user, "Alpha", "alpha");
        await SeedMembershipAsync(user, "Beta", "beta");

        var result = await _handler.Handle(new GetMyWorkspacesQuery(user.Id), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UserWithNoWorkspaces_ReturnsEmpty()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(new GetMyWorkspacesQuery(user.Id), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleWorkspaces_OrderedByName()
    {
        var user = await SeedUserAsync();
        await SeedMembershipAsync(user, "Zebra", "zebra");
        await SeedMembershipAsync(user, "Alpha", "alpha");
        await SeedMembershipAsync(user, "Mango", "mango");

        var result = await _handler.Handle(new GetMyWorkspacesQuery(user.Id), CancellationToken.None);

        result.Select(w => w.Name).Should().BeInAscendingOrder();
    }
}
