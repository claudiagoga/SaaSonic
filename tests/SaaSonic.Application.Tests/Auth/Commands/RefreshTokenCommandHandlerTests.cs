using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class RefreshTokenCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new RefreshTokenCommandHandler(_db, _jwtService.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(User user, string rawToken)> SeedValidTokenAsync(
        bool isUserActive = true,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? revokedAt = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
            IsActive = isUserActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);

        const string rawToken = "my-raw-refresh-token";
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TestHashHelper.Hash(rawToken),
            ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(30),
            RevokedAt = revokedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _db.SaveChangesAsync();
        return (user, rawToken);
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidToken_ReturnsNewTokenPair()
    {
        var (user, rawToken) = await SeedValidTokenAsync();
        _jwtService.Setup(j => j.GenerateAccessToken(user.Id, user.Email)).Returns("new-access");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(("new-raw-refresh", "new-hashed-refresh"));

        var result = await _handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-raw-refresh");
        result.TokenType.Should().Be("Bearer");
    }

    [Fact]
    public async Task Handle_ValidToken_OldTokenIsRevoked()
    {
        var (_, rawToken) = await SeedValidTokenAsync();
        _jwtService.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>())).Returns("t");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(("new-raw", "new-hash"));

        await _handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        var oldToken = await _db.RefreshTokens
            .FirstAsync(rt => rt.TokenHash == TestHashHelper.Hash(rawToken));
        oldToken.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ValidToken_NewTokenIsAddedToDatabase()
    {
        var (_, rawToken) = await SeedValidTokenAsync();
        _jwtService.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>())).Returns("t");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(("new-raw", "new-hash"));

        await _handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        _db.RefreshTokens.Should().HaveCount(2);
    }

    // ── failure paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsUnauthorizedException()
    {
        // Pass a token whose hash does not exist in the database.
        var act = () => _handler.Handle(
            new RefreshTokenCommand("token-that-does-not-exist"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_RevokedToken_ThrowsUnauthorizedException()
    {
        // A token that was already revoked indicates a reuse attack.
        var (_, rawToken) = await SeedValidTokenAsync(revokedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var act = () => _handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*revoked*");
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsUnauthorizedException()
    {
        var (_, rawToken) = await SeedValidTokenAsync(
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var act = () => _handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*expired*");
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsUnauthorizedException()
    {
        var (_, rawToken) = await SeedValidTokenAsync(isUserActive: false);

        var act = () => _handler.Handle(new RefreshTokenCommand(rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task Handle_RevokedToken_RevokesChildTokens()
    {
        // Token reuse detection: if token A was already replaced by token B,
        // and someone replays token A, token B must also be revoked.
        var (user, _) = await SeedValidTokenAsync();

        const string parentRawToken = "parent-token";
        const string childRawToken = "child-token";

        var childToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TestHashHelper.Hash(childRawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var parentToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TestHashHelper.Hash(parentRawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAt = DateTimeOffset.UtcNow.AddHours(-1), // already revoked
            ReplacedByTokenId = childToken.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.RefreshTokens.Add(childToken);
        _db.RefreshTokens.Add(parentToken);
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(new RefreshTokenCommand(parentRawToken), CancellationToken.None);
        await act.Should().ThrowAsync<UnauthorizedException>();

        // The child token must have been revoked as part of family revocation.
        var child = await _db.RefreshTokens.FindAsync(childToken.Id);
        child!.RevokedAt.Should().NotBeNull();
    }
}
