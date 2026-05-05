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

public class LoginCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new LoginCommandHandler(_db, _passwordHasher.Object, _jwtService.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<User> SeedLocalUserAsync(
        string email = "user@test.com",
        bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
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
            ProviderUserId = email,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        return user;
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokensAndUserInfo()
    {
        // Arrange
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify("Password1!", user.PasswordHash!)).Returns(true);
        _jwtService.Setup(j => j.GenerateAccessToken(user.Id, user.Email)).Returns("access-token");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(("raw-refresh", "hashed-refresh"));

        // Act
        var result = await _handler.Handle(
            new LoginCommand(user.Email, "Password1!"), CancellationToken.None);

        // Assert
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh");
        result.TokenType.Should().Be("Bearer");
        result.User.Email.Should().Be(user.Email);
        result.User.Id.Should().Be(user.Id);

        // A refresh token row must have been saved to the database
        _db.RefreshTokens.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ValidCredentials_EmailIsCaseInsensitive()
    {
        // The handler normalises the email to lower-case before querying.
        // Sending "USER@TEST.COM" should still find the "user@test.com" account.
        var user = await SeedLocalUserAsync("user@test.com");
        _passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        _jwtService.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>())).Returns("t");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(("r", "h"));

        var result = await _handler.Handle(
            new LoginCommand("USER@TEST.COM", "Password1!"), CancellationToken.None);

        result.Should().NotBeNull();
    }

    // ── failure paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        // No user seeded — the DB is empty.
        var act = () => _handler.Handle(
            new LoginCommand("nobody@test.com", "Password1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsUnauthorizedException()
    {
        await SeedLocalUserAsync(isActive: false);

        var act = () => _handler.Handle(
            new LoginCommand("user@test.com", "Password1!"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_SocialOnlyAccount_ThrowsUnauthorizedException()
    {
        // A user whose only AuthIdentity is Google has no local password.
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
            new LoginCommand("social@test.com", "anything"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*social login*");
    }

    [Fact]
    public async Task Handle_WrongPassword_ThrowsUnauthorizedException()
    {
        var user = await SeedLocalUserAsync();
        _passwordHasher.Setup(h => h.Verify("WrongPassword", user.PasswordHash!)).Returns(false);

        var act = () => _handler.Handle(
            new LoginCommand(user.Email, "WrongPassword"), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
