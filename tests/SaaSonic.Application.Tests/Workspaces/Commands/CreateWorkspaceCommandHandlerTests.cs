using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Workspaces.Commands;
using SaaSonic.Domain.Constants;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Workspaces.Commands;

public class CreateWorkspaceCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly CreateWorkspaceCommandHandler _handler;

    public CreateWorkspaceCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new CreateWorkspaceCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "owner@test.com",
            DisplayName = "Owner",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidInput_CreatesWorkspace()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new CreateWorkspaceCommand(user.Id, "My Workspace", "my-workspace"), CancellationToken.None);

        result.Name.Should().Be("My Workspace");
        result.Slug.Should().Be("my-workspace");
        result.OwnerUserId.Should().Be(user.Id);

        var workspace = await _db.Workspaces.FirstOrDefaultAsync(w => w.Slug == "my-workspace");
        workspace.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidInput_CreatesOwnerMembership()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new CreateWorkspaceCommand(user.Id, "My Workspace", "my-workspace"), CancellationToken.None);

        var membership = await _db.WorkspaceMembers
            .FirstOrDefaultAsync(m => m.WorkspaceId == result.Id && m.UserId == user.Id);

        membership.Should().NotBeNull();
        membership!.RoleId.Should().Be(RoleIds.Owner);
        membership.MembershipStatus.Should().Be(MembershipStatus.Active);
    }

    [Fact]
    public async Task Handle_ValidInput_ReturnsOwnerRole()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new CreateWorkspaceCommand(user.Id, "My Workspace", "my-workspace"), CancellationToken.None);

        result.UserRole.Should().Be(RoleNames.Owner);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateSlug_ThrowsValidationException()
    {
        var user = await SeedUserAsync();
        _db.Workspaces.Add(new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Existing",
            Slug = "my-workspace",
            OwnerUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new CreateWorkspaceCommand(user.Id, "Another", "my-workspace"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*slug*");
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        var act = () => _handler.Handle(
            new CreateWorkspaceCommand(Guid.NewGuid(), "My Workspace", "my-workspace"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
