using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Common.Constants;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class RegisterCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IJwtService> _jwtService = new();
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);

        _configuration.Setup(c => c["App:FrontendUrl"]).Returns("http://localhost:3000");
        _passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");
        _jwtService.Setup(j => j.GenerateAccessToken(It.IsAny<Guid>(), It.IsAny<string>())).Returns("access-token");
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(("raw-refresh", "hashed-refresh"));

        _handler = new RegisterCommandHandler(
            _db, _passwordHasher.Object, _jwtService.Object,
            _emailQueue.Object, _configuration.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_NewEmail_CreatesUserAndReturnsTokens()
    {
        var command = new RegisterCommand("new@test.com", "Password1!", "Alice");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("raw-refresh");
        result.RequiresEmailVerification.Should().BeTrue();
        result.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_NewEmail_SavesUserWithCorrectFields()
    {
        await _handler.Handle(new RegisterCommand("alice@test.com", "Password1!", "Alice"), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.Email.Should().Be("alice@test.com");
        user.DisplayName.Should().Be("Alice");
        user.PasswordHash.Should().Be("hashed-password");
        user.EmailVerified.Should().BeFalse();
        user.IsActive.Should().BeTrue();
        user.EmailVerificationTokenHash.Should().NotBeNullOrEmpty();
        user.EmailVerificationTokenExpiry.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_NewEmail_CreatesLocalAuthIdentity()
    {
        await _handler.Handle(new RegisterCommand("alice@test.com", "Password1!", "Alice"), CancellationToken.None);

        var identity = await _db.AuthIdentities.SingleAsync();
        identity.Provider.Should().Be(AuthProvider.Local);
        identity.ProviderUserId.Should().Be("alice@test.com");
    }

    [Fact]
    public async Task Handle_NewEmail_SendsVerificationEmail()
    {
        await _handler.Handle(new RegisterCommand("alice@test.com", "Password1!", "Alice"), CancellationToken.None);

        _emailQueue.Verify(q => q.Enqueue(
            "alice@test.com",
            EmailTemplateSlug.EmailVerification,
            It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NewEmail_EmailIsNormalisedToLowerCase()
    {
        await _handler.Handle(new RegisterCommand("ALICE@TEST.COM", "Password1!", "Alice"), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.Email.Should().Be("alice@test.com");
    }

    // ── failure paths ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsValidationException()
    {
        // Seed an existing user with the same email
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            DisplayName = "Existing",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var act = () => _handler.Handle(
            new RegisterCommand("existing@test.com", "Password1!", "Bob"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task Handle_DuplicateEmail_IsCaseInsensitive()
    {
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@test.com",
            DisplayName = "Existing",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        // Sending upper-case version of the same email should still be rejected.
        var act = () => _handler.Handle(
            new RegisterCommand("EXISTING@TEST.COM", "Password1!", "Bob"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
