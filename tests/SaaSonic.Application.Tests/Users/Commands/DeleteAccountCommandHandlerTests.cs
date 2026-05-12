using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Users.Commands;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Users.Commands;

public class DeleteAccountCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly DeleteAccountCommandHandler _handler;

    public DeleteAccountCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new DeleteAccountCommandHandler(_db, _passwordHasher.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedLocalUserAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
            PasswordHash = "hashed-password",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        _db.AuthIdentities.Add(new AuthIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = AuthProvider.Local,
            ProviderUserId = user.Email,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return user;
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidPassword_DeactivatesUser()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify("Password1!", user.PasswordHash!)).Returns(true);

        await _handler.Handle(new DeleteAccountCommand(user.Id, "Password1!"), CancellationToken.None);

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidPassword_AnonymisesPii()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await _handler.Handle(new DeleteAccountCommand(user.Id, "Password1!"), CancellationToken.None);

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.Email.Should().Contain("deleted");
        updated.DisplayName.Should().Be("Deleted User");
        updated.AvatarUrl.Should().BeNull();
        updated.PasswordHash.Should().Be(string.Empty);
    }

    [Fact]
    public async Task Handle_ValidPassword_RemovesAuthIdentities()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await _handler.Handle(new DeleteAccountCommand(user.Id, "Password1!"), CancellationToken.None);

        var identities = await _db.AuthIdentities.Where(a => a.UserId == user.Id).ToListAsync();
        identities.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ValidPassword_RemovesRefreshTokens()
    {
        var user = await SeedLocalUserAsync();
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "token-hash",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        await _handler.Handle(new DeleteAccountCommand(user.Id, "Password1!"), CancellationToken.None);

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        tokens.Should().BeEmpty();
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserWithOwnedWorkspaces_ThrowsValidationException()
    {
        var user = await SeedLocalUserAsync();
        _db.Workspaces.Add(new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "My Workspace",
            Slug = "my-workspace",
            OwnerUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);

        var act = () => _handler.Handle(new DeleteAccountCommand(user.Id, "Password1!"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*workspaces*");
    }

    [Fact]
    public async Task Handle_SocialAccount_ThrowsValidationException()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "social@test.com",
            DisplayName = "Social User",
            PasswordHash = null,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(new DeleteAccountCommand(user.Id, "Password1!"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedException()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify("WrongPass", user.PasswordHash!)).Returns(false);

        var act = () => _handler.Handle(new DeleteAccountCommand(user.Id, "WrongPass"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        var act = () => _handler.Handle(new DeleteAccountCommand(Guid.NewGuid(), "Password1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
