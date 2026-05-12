using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class ChangePasswordCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly ChangePasswordCommandHandler _handler;

    public ChangePasswordCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new ChangePasswordCommandHandler(_db, _passwordHasher.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedLocalUserAsync(bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
            PasswordHash = "hashed-password",
            EmailVerified = true,
            IsActive = isActive,
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

    private async Task SeedRefreshTokenAsync(Guid userId, bool revoked = false)
    {
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = revoked ? DateTimeOffset.UtcNow : null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidPassword_UpdatesPasswordHash()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify("OldPass1!", user.PasswordHash!)).Returns(true);
        _passwordHasher.Setup(h => h.Hash("NewPass1!")).Returns("new-hashed-password");

        await _handler.Handle(new ChangePasswordCommand(user.Id, "OldPass1!", "NewPass1!"), CancellationToken.None);

        var updated = await _db.Users.FindAsync(user.Id);
        updated!.PasswordHash.Should().Be("new-hashed-password");
    }

    [Fact]
    public async Task Handle_ValidPassword_RevokesAllActiveSessions()
    {
        var user = await SeedLocalUserAsync();
        await SeedRefreshTokenAsync(user.Id);
        await SeedRefreshTokenAsync(user.Id);
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("new-hash");

        await _handler.Handle(new ChangePasswordCommand(user.Id, "OldPass1!", "NewPass1!"), CancellationToken.None);

        var tokens = await _db.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        tokens.Should().AllSatisfy(t => t.RevokedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_ValidPassword_DoesNotOverwriteAlreadyRevokedTimestamp()
    {
        var user = await SeedLocalUserAsync();
        var pastRevocation = DateTimeOffset.UtcNow.AddDays(-1);
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "already-revoked",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            RevokedAt = pastRevocation,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("new-hash");

        await _handler.Handle(new ChangePasswordCommand(user.Id, "OldPass1!", "NewPass1!"), CancellationToken.None);

        var token = await _db.RefreshTokens.FirstAsync(t => t.TokenHash == "already-revoked");
        token.RevokedAt.Should().Be(pastRevocation);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        var act = () => _handler.Handle(
            new ChangePasswordCommand(Guid.NewGuid(), "OldPass1!", "NewPass1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
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
        _db.AuthIdentities.Add(new AuthIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = AuthProvider.Google,
            ProviderUserId = "google-123",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new ChangePasswordCommand(user.Id, "OldPass1!", "NewPass1!"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Handle_WrongCurrentPassword_ThrowsUnauthorizedException()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify("WrongPass1!", user.PasswordHash!)).Returns(false);

        var act = () => _handler.Handle(
            new ChangePasswordCommand(user.Id, "WrongPass1!", "NewPass1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
