using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class VerifyEmailCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly VerifyEmailCommandHandler _handler;

    public VerifyEmailCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new VerifyEmailCommandHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<(User user, string rawToken)> SeedUnverifiedUserAsync(
        DateTimeOffset? tokenExpiry = null,
        bool emailVerified = false)
    {
        const string rawToken = "valid-verification-token";

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
            IsActive = true,
            EmailVerified = emailVerified,
            EmailVerificationTokenHash = TestHashHelper.Hash(rawToken),
            EmailVerificationTokenExpiry = tokenExpiry ?? DateTimeOffset.UtcNow.AddDays(3),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return (user, rawToken);
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidToken_SetsEmailVerifiedToTrue()
    {
        var (_, rawToken) = await SeedUnverifiedUserAsync();

        await _handler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidToken_ClearsVerificationTokenFields()
    {
        var (_, rawToken) = await SeedUnverifiedUserAsync();

        await _handler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.EmailVerificationTokenHash.Should().BeNull();
        user.EmailVerificationTokenExpiry.Should().BeNull();
    }

    [Fact]
    public async Task Handle_AlreadyVerifiedUser_CompletesWithoutError()
    {
        // Clicking the email link twice (e.g. double-click) should not throw.
        // The handler clears the token and returns gracefully.
        var (_, rawToken) = await SeedUnverifiedUserAsync(emailVerified: true);

        var act = () => _handler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_AlreadyVerifiedUser_ClearsTokenFields()
    {
        var (_, rawToken) = await SeedUnverifiedUserAsync(emailVerified: true);

        await _handler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.EmailVerificationTokenHash.Should().BeNull();
        user.EmailVerificationTokenExpiry.Should().BeNull();
    }

    // ── failure paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_InvalidToken_ThrowsUnauthorizedException()
    {
        await SeedUnverifiedUserAsync();

        var act = () => _handler.Handle(
            new VerifyEmailCommand("completely-wrong-token"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsUnauthorizedException()
    {
        var (_, rawToken) = await SeedUnverifiedUserAsync(
            tokenExpiry: DateTimeOffset.UtcNow.AddDays(-1));

        var act = () => _handler.Handle(new VerifyEmailCommand(rawToken), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
