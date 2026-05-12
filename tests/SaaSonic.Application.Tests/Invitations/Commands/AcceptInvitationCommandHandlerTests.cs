using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Invitations.Commands;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Invitations.Commands;

public class AcceptInvitationCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly AcceptInvitationCommandHandler _handler;

    public AcceptInvitationCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new AcceptInvitationCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private const string RawToken = "test-raw-token";
    private static readonly string TokenHash = TestHashHelper.Hash(RawToken);

    private async Task<(User invitee, WorkspaceInvitation invitation)> SeedPendingInvitationAsync(
        string inviteeEmail = "invitee@test.com",
        bool expired = false)
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
        var invitee = new User
        {
            Id = Guid.NewGuid(),
            Email = inviteeEmail,
            DisplayName = "Invitee",
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
            Email = inviteeEmail,
            RoleId = RoleIds.Editor,
            Status = WorkspaceInvitationStatus.Pending,
            TokenHash = TokenHash,
            ExpiresAt = expired
                ? DateTimeOffset.UtcNow.AddDays(-1)
                : DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.AddRange(owner, invitee);
        _db.Workspaces.Add(workspace);
        _db.WorkspaceInvitations.Add(invitation);
        await _db.SaveChangesAsync();
        return (invitee, invitation);
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidToken_CreatesMembership()
    {
        var (invitee, invitation) = await SeedPendingInvitationAsync();

        await _handler.Handle(new AcceptInvitationCommand(invitee.Id, RawToken), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.UserId == invitee.Id && m.WorkspaceId == invitation.WorkspaceId);
        membership.Should().NotBeNull();
        membership!.RoleId.Should().Be(RoleIds.Editor);
        membership.MembershipStatus.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Handle_ValidToken_MarksInvitationAccepted()
    {
        var (invitee, invitation) = await SeedPendingInvitationAsync();

        await _handler.Handle(new AcceptInvitationCommand(invitee.Id, RawToken), CancellationToken.None);

        var updated = await _db.WorkspaceInvitations.FindAsync(invitation.Id);
        updated!.Status.Should().Be(WorkspaceInvitationStatus.Accepted);
    }

    [Fact]
    public async Task Handle_AlreadyMember_MarksAcceptedWithoutDuplicateMembership()
    {
        var (invitee, invitation) = await SeedPendingInvitationAsync();
        _db.WorkspaceMembers.Add(new WorkspaceMember
        {
            Id = Guid.NewGuid(),
            WorkspaceId = invitation.WorkspaceId,
            UserId = invitee.Id,
            RoleId = RoleIds.Editor,
            MembershipStatus = MembershipStatus.Active,
            JoinedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _handler.Handle(new AcceptInvitationCommand(invitee.Id, RawToken), CancellationToken.None);

        var memberships = await _db.WorkspaceMembers
            .Where(m => m.UserId == invitee.Id && m.WorkspaceId == invitation.WorkspaceId).ToListAsync();
        memberships.Should().HaveCount(1);
        var updated = await _db.WorkspaceInvitations.FindAsync(invitation.Id);
        updated!.Status.Should().Be(WorkspaceInvitationStatus.Accepted);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InvalidToken_ThrowsForbiddenException()
    {
        var (invitee, _) = await SeedPendingInvitationAsync();

        var act = () => _handler.Handle(
            new AcceptInvitationCommand(invitee.Id, "wrong-token"), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsForbiddenException()
    {
        var (invitee, _) = await SeedPendingInvitationAsync(expired: true);

        var act = () => _handler.Handle(
            new AcceptInvitationCommand(invitee.Id, RawToken), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_WrongEmailUser_ThrowsForbiddenException()
    {
        var (_, invitation) = await SeedPendingInvitationAsync("invitee@test.com");
        var wrongUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "other@test.com",
            DisplayName = "Other",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(wrongUser);
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new AcceptInvitationCommand(wrongUser.Id, RawToken), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_AlreadyAcceptedToken_ThrowsForbiddenException()
    {
        var (invitee, invitation) = await SeedPendingInvitationAsync();

        // Accept once
        await _handler.Handle(new AcceptInvitationCommand(invitee.Id, RawToken), CancellationToken.None);

        // Try to accept again
        var act = () => _handler.Handle(
            new AcceptInvitationCommand(invitee.Id, RawToken), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
