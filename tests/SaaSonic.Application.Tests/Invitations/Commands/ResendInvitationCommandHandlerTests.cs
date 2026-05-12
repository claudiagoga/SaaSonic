using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Invitations.Commands;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Invitations.Commands;

public class ResendInvitationCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly ResendInvitationCommandHandler _handler;

    public ResendInvitationCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new ResendInvitationCommandHandler(_db, _emailQueue.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(User owner, Workspace workspace, WorkspaceInvitation invitation)> SeedAsync(
        WorkspaceInvitationStatus status = WorkspaceInvitationStatus.Pending)
    {
        _db.Roles.Add(new Role { Id = RoleIds.Editor, Name = RoleNames.Editor, Scope = RoleScope.Workspace });

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
        var invitation = new WorkspaceInvitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            InvitedByUserId = owner.Id,
            Email = "invitee@test.com",
            RoleId = RoleIds.Editor,
            Status = status,
            TokenHash = "original-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(3),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(owner);
        _db.Workspaces.Add(workspace);
        _db.WorkspaceInvitations.Add(invitation);
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
        return (owner, workspace, invitation);
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PendingInvitation_RegeneratesToken()
    {
        var (owner, workspace, invitation) = await SeedAsync();
        var originalHash = invitation.TokenHash;

        await _handler.Handle(
            new ResendInvitationCommand(workspace.Id, owner.Id, invitation.Id), CancellationToken.None);

        var updated = await _db.WorkspaceInvitations.FindAsync(invitation.Id);
        updated!.TokenHash.Should().NotBe(originalHash);
    }

    [Fact]
    public async Task Handle_PendingInvitation_ResetsExpiryToSevenDays()
    {
        var (owner, workspace, invitation) = await SeedAsync();
        var before = DateTimeOffset.UtcNow.AddDays(7).AddSeconds(-5);

        await _handler.Handle(
            new ResendInvitationCommand(workspace.Id, owner.Id, invitation.Id), CancellationToken.None);

        var updated = await _db.WorkspaceInvitations.FindAsync(invitation.Id);
        updated!.ExpiresAt.Should().BeAfter(before);
    }

    [Fact]
    public async Task Handle_PendingInvitation_SendsEmail()
    {
        var (owner, workspace, invitation) = await SeedAsync();

        await _handler.Handle(
            new ResendInvitationCommand(workspace.Id, owner.Id, invitation.Id), CancellationToken.None);

        _emailQueue.Verify(
            q => q.Enqueue(invitation.Email, It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
            Times.Once);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AcceptedInvitation_ThrowsForbiddenException()
    {
        var (owner, workspace, invitation) = await SeedAsync(WorkspaceInvitationStatus.Accepted);

        var act = () => _handler.Handle(
            new ResendInvitationCommand(workspace.Id, owner.Id, invitation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_RevokedInvitation_ThrowsForbiddenException()
    {
        var (owner, workspace, invitation) = await SeedAsync(WorkspaceInvitationStatus.Revoked);

        var act = () => _handler.Handle(
            new ResendInvitationCommand(workspace.Id, owner.Id, invitation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_InvitationNotFound_ThrowsForbiddenException()
    {
        var (owner, workspace, _) = await SeedAsync();

        var act = () => _handler.Handle(
            new ResendInvitationCommand(workspace.Id, owner.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_EditorAttempts_ThrowsForbiddenException()
    {
        var (owner, workspace, invitation) = await SeedAsync();
        var editor = new User
        {
            Id = Guid.NewGuid(),
            Email = "editor@test.com",
            DisplayName = "Editor",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(editor);
        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = editor.Id,
            RoleId = RoleIds.Editor,
            MembershipStatus = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new ResendInvitationCommand(workspace.Id, editor.Id, invitation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
