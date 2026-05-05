using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class LogoutCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new LogoutCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<string> SeedTokenAsync(DateTimeOffset? revokedAt = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);

        const string rawToken = "my-refresh-token";
        _db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = TestHashHelper.Hash(rawToken),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RevokedAt = revokedAt,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return rawToken;
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveToken_RevokesIt()
    {
        var rawToken = await SeedTokenAsync();

        await _handler.Handle(new LogoutCommand(rawToken), CancellationToken.None);

        var token = await _db.RefreshTokens.SingleAsync();
        token.RevokedAt.Should().NotBeNull();
    }

    // ── idempotency / edge cases ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_TokenNotFound_CompletesWithoutError()
    {
        // Passing a token that doesn't exist should silently no-op.
        var act = () => _handler.Handle(
            new LogoutCommand("token-that-does-not-exist"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_AlreadyRevokedToken_DoesNotDoubleRevoke()
    {
        // If the token was already revoked (e.g. previous logout), the existing
        // RevokedAt timestamp should not be overwritten.
        var originalRevocationTime = DateTimeOffset.UtcNow.AddHours(-2);
        var rawToken = await SeedTokenAsync(revokedAt: originalRevocationTime);

        await _handler.Handle(new LogoutCommand(rawToken), CancellationToken.None);

        var token = await _db.RefreshTokens.SingleAsync();
        token.RevokedAt.Should().Be(originalRevocationTime);
    }
}
