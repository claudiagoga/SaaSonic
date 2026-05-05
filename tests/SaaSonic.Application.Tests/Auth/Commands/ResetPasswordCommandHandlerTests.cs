using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class ResetPasswordCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("new-hashed-password");
        _handler = new ResetPasswordCommandHandler(_db, _passwordHasher.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(User user, string rawToken)> SeedUserWithResetTokenAsync(
        DateTimeOffset? tokenExpiry = null)
    {
        const string rawToken = "valid-reset-token";
        var tokenHash = TestHashHelper.Hash(rawToken);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
            PasswordHash = "old-hashed-password",
            IsActive = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetTokenExpiry = tokenExpiry ?? DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (user, rawToken);
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidToken_UpdatesPasswordHash()
    {
        var (_, rawToken) = await SeedUserWithResetTokenAsync();

        await _handler.Handle(new ResetPasswordCommand(rawToken, "NewPassword1!"), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.PasswordHash.Should().Be("new-hashed-password");
    }

    [Fact]
    public async Task Handle_ValidToken_ClearsResetTokenFields()
    {
        var (_, rawToken) = await SeedUserWithResetTokenAsync();

        await _handler.Handle(new ResetPasswordCommand(rawToken, "NewPassword1!"), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.PasswordResetTokenHash.Should().BeNull();
        user.PasswordResetTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ValidToken_RevokesAllActiveRefreshTokens()
    {
        // After a password reset, all existing sessions must be invalidated.
        var (user, rawToken) = await SeedUserWithResetTokenAsync();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash-1",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash-2",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _handler.Handle(new ResetPasswordCommand(rawToken, "NewPassword1!"), CancellationToken.None);

        var allTokens = await _db.RefreshTokens.ToListAsync();
        allTokens.Should().AllSatisfy(t => t.RevokedAt.Should().NotBeNull());
    }

    [Fact]
    public async Task Handle_ValidToken_DoesNotRevokeAlreadyRevokedTokens()
    {
        // Already-revoked tokens should be left as-is (their existing RevokedAt preserved).
        var (user, rawToken) = await SeedUserWithResetTokenAsync();
        var alreadyRevokedAt = DateTimeOffset.UtcNow.AddDays(-5);

        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "already-revoked",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAt = alreadyRevokedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await _handler.Handle(new ResetPasswordCommand(rawToken, "NewPassword1!"), CancellationToken.None);

        // The query in the handler filters rt.RevokedAt == null, so this pre-revoked
        // token should not have been touched.
        var token = await _db.RefreshTokens.SingleAsync();
        token.RevokedAt.Should().Be(alreadyRevokedAt);
    }

    // ── failure paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InvalidToken_ThrowsUnauthorizedException()
    {
        await SeedUserWithResetTokenAsync();

        var act = () => _handler.Handle(
            new ResetPasswordCommand("completely-wrong-token", "NewPassword1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsUnauthorizedException()
    {
        var (_, rawToken) = await SeedUserWithResetTokenAsync(
            tokenExpiry: DateTimeOffset.UtcNow.AddHours(-1));

        var act = () => _handler.Handle(
            new ResetPasswordCommand(rawToken, "NewPassword1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
