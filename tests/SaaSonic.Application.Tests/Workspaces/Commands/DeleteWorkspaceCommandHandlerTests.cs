using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Workspaces.Commands;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Workspaces.Commands;

public class DeleteWorkspaceCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly DeleteWorkspaceCommandHandler _handler;

    public DeleteWorkspaceCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new DeleteWorkspaceCommandHandler(_db);
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
            Name = "My Workspace",
            Slug = "my-workspace",
            OwnerUserId = owner.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(owner);
        _db.Workspaces.Add(workspace);
        await _db.SaveChangesAsync();
        return (owner, workspace);
    }

    private async Task AddPendingInvitationAsync(Workspace workspace, User owner)
    {
        _db.WorkspaceInvitations.Add(new WorkspaceInvitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            InvitedByUserId = owner.Id,
            Email = "invitee@test.com",
            RoleId = RoleIds.Editor,
            Status = WorkspaceInvitationStatus.Pending,
            TokenHash = "token-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_OwnerDeletesWorkspace_SoftDeletes()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        await _handler.Handle(new DeleteWorkspaceCommand(owner.Id, workspace.Id), CancellationToken.None);

        var updated = await _db.Workspaces.FindAsync(workspace.Id);
        updated!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_OwnerDeletesWorkspace_MangleSlug()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        await _handler.Handle(new DeleteWorkspaceCommand(owner.Id, workspace.Id), CancellationToken.None);

        var updated = await _db.Workspaces.FindAsync(workspace.Id);
        updated!.Slug.Should().Contain("deleted");
        updated.Slug.Should().NotBe("my-workspace");
    }

    [Fact]
    public async Task Handle_OwnerDeletesWorkspace_RevokesPendingInvitations()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        await AddPendingInvitationAsync(workspace, owner);

        await _handler.Handle(new DeleteWorkspaceCommand(owner.Id, workspace.Id), CancellationToken.None);

        var invitations = await _db.WorkspaceInvitations
            .Where(i => i.WorkspaceId == workspace.Id).ToListAsync();
        invitations.Should().AllSatisfy(i =>
            i.Status.Should().Be(WorkspaceInvitationStatus.Revoked));
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NonOwner_ThrowsForbiddenException()
    {
        var (_, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new DeleteWorkspaceCommand(Guid.NewGuid(), workspace.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_WorkspaceNotFound_ThrowsForbiddenException()
    {
        var act = () => _handler.Handle(
            new DeleteWorkspaceCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_ActiveSubscription_ThrowsValidationException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        _db.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new DeleteWorkspaceCommand(owner.Id, workspace.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*subscription*");
    }
}
