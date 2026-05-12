using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Members.Commands;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Members.Commands;

public class TransferOwnershipCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly TransferOwnershipCommandHandler _handler;

    public TransferOwnershipCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new TransferOwnershipCommandHandler(_db);
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
    public async Task Handle_ValidTransfer_UpdatesWorkspaceOwner()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var newOwner = await AddMemberAsync(workspace, RoleIds.Editor);

        await _handler.Handle(
            new TransferOwnershipCommand(workspace.Id, owner.Id, newOwner.Id), CancellationToken.None);

        var updated = await _db.Workspaces.FindAsync(workspace.Id);
        updated!.OwnerUserId.Should().Be(newOwner.Id);
    }

    [Fact]
    public async Task Handle_ValidTransfer_NewOwnerGetsOwnerRole()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var newOwner = await AddMemberAsync(workspace, RoleIds.Editor);

        await _handler.Handle(
            new TransferOwnershipCommand(workspace.Id, owner.Id, newOwner.Id), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstAsync(m => m.UserId == newOwner.Id && m.WorkspaceId == workspace.Id);
        membership.RoleId.Should().Be(RoleIds.Owner);
    }

    [Fact]
    public async Task Handle_ValidTransfer_OldOwnerBecomesAdmin()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var newOwner = await AddMemberAsync(workspace, RoleIds.Editor);

        await _handler.Handle(
            new TransferOwnershipCommand(workspace.Id, owner.Id, newOwner.Id), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstAsync(m => m.UserId == owner.Id && m.WorkspaceId == workspace.Id);
        membership.RoleId.Should().Be(RoleIds.Admin);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");
        var target = await AddMemberAsync(workspace, RoleIds.Viewer, "viewer@test.com");

        var act = () => _handler.Handle(
            new TransferOwnershipCommand(workspace.Id, editor.Id, target.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_TransferToSelf_ThrowsValidationException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new TransferOwnershipCommand(workspace.Id, owner.Id, owner.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_TargetNotMember_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new TransferOwnershipCommand(workspace.Id, owner.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_WorkspaceNotFound_ThrowsForbiddenException()
    {
        var act = () => _handler.Handle(
            new TransferOwnershipCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
