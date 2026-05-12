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

public class InviteMemberCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly InviteMemberCommandHandler _handler;

    public InviteMemberCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new InviteMemberCommandHandler(_db, _emailQueue.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<(User owner, Workspace workspace)> SeedWorkspaceAsync()
    {
        _db.Roles.AddRange(
            new Role { Id = RoleIds.Owner,  Name = RoleNames.Owner,  Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Admin,  Name = RoleNames.Admin,  Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Editor, Name = RoleNames.Editor, Scope = RoleScope.Workspace },
            new Role { Id = RoleIds.Viewer, Name = RoleNames.Viewer, Scope = RoleScope.Workspace }
        );
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

    private async Task<User> AddMemberAsync(Workspace workspace, Guid roleId, string email)
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
    public async Task Handle_OwnerInvitesEditor_CreatesInvitation()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var result = await _handler.Handle(
            new InviteMemberCommand(workspace.Id, owner.Id, "invitee@test.com", RoleIds.Editor), CancellationToken.None);

        result.Email.Should().Be("invitee@test.com");
        result.Role.Should().Be(RoleNames.Editor);

        var invitation = await _db.WorkspaceInvitations
            .FirstOrDefaultAsync(i => i.Email == "invitee@test.com");
        invitation.Should().NotBeNull();
        invitation!.Status.Should().Be(WorkspaceInvitationStatus.Pending);
    }

    [Fact]
    public async Task Handle_OwnerInvitesAdmin_CreatesInvitation()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var result = await _handler.Handle(
            new InviteMemberCommand(workspace.Id, owner.Id, "admin@test.com", RoleIds.Admin), CancellationToken.None);

        result.Role.Should().Be(RoleNames.Admin);
    }

    [Fact]
    public async Task Handle_ValidInvite_SendsEmail()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        await _handler.Handle(
            new InviteMemberCommand(workspace.Id, owner.Id, "invitee@test.com", RoleIds.Editor), CancellationToken.None);

        _emailQueue.Verify(
            q => q.Enqueue("invitee@test.com", It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AdminInvitesViewer_CreatesInvitation()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin = await AddMemberAsync(workspace, RoleIds.Admin, "admin@test.com");

        var result = await _handler.Handle(
            new InviteMemberCommand(workspace.Id, admin.Id, "viewer@test.com", RoleIds.Viewer), CancellationToken.None);

        result.Role.Should().Be(RoleNames.Viewer);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_AdminInvitesAdmin_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var admin = await AddMemberAsync(workspace, RoleIds.Admin, "admin@test.com");

        var act = () => _handler.Handle(
            new InviteMemberCommand(workspace.Id, admin.Id, "newadmin@test.com", RoleIds.Admin), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AlreadyMember_ThrowsValidationException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        await AddMemberAsync(workspace, RoleIds.Editor, "existing@test.com");

        var act = () => _handler.Handle(
            new InviteMemberCommand(workspace.Id, owner.Id, "existing@test.com", RoleIds.Editor), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_PendingInvitationExists_ThrowsValidationException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        _db.WorkspaceInvitations.Add(new WorkspaceInvitation
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            InvitedByUserId = owner.Id,
            Email = "pending@test.com",
            RoleId = RoleIds.Editor,
            Status = WorkspaceInvitationStatus.Pending,
            TokenHash = "existing-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new InviteMemberCommand(workspace.Id, owner.Id, "pending@test.com", RoleIds.Editor), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_EditorAttempts_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();
        var editor = await AddMemberAsync(workspace, RoleIds.Editor, "editor@test.com");

        var act = () => _handler.Handle(
            new InviteMemberCommand(workspace.Id, editor.Id, "invitee@test.com", RoleIds.Viewer), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NonMember_ThrowsForbiddenException()
    {
        var (owner, workspace) = await SeedWorkspaceAsync();

        var act = () => _handler.Handle(
            new InviteMemberCommand(workspace.Id, Guid.NewGuid(), "invitee@test.com", RoleIds.Editor), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
