using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Users.Commands;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Users.Commands;

public class UpdateUserCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly UpdateUserCommandHandler _handler;

    public UpdateUserCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);

        var config = new Mock<IConfiguration>();
        config.Setup(c => c["App:EmailVerificationUrl"]).Returns("http://localhost/verify");

        _handler = new UpdateUserCommandHandler(_db, _emailQueue.Object, config.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(string email = "user@test.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Original Name",
            AvatarUrl = "https://example.com/avatar.png",
            EmailVerified = true,
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
    public async Task Handle_UpdateDisplayName_UpdatesUser()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new UpdateUserCommand(user.Id, "New Name", null, null), CancellationToken.None);

        result.DisplayName.Should().Be("New Name");
        var updated = await _db.Users.FindAsync(user.Id);
        updated!.DisplayName.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_UpdateAvatarUrl_UpdatesAvatar()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new UpdateUserCommand(user.Id, null, "https://example.com/new.png", null), CancellationToken.None);

        result.AvatarUrl.Should().Be("https://example.com/new.png");
    }

    [Fact]
    public async Task Handle_ClearAvatarUrl_SetsAvatarToNull()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new UpdateUserCommand(user.Id, null, string.Empty, null), CancellationToken.None);

        result.AvatarUrl.Should().BeNull();
        var updated = await _db.Users.FindAsync(user.Id);
        updated!.AvatarUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_UpdateEmail_ChangesEmailAndRequiresReverification()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(
            new UpdateUserCommand(user.Id, null, null, "new@test.com"), CancellationToken.None);

        result.Email.Should().Be("new@test.com");
        result.EmailVerified.Should().BeFalse();
        var updated = await _db.Users.FindAsync(user.Id);
        updated!.EmailVerificationTokenHash.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_UpdateEmail_SendsVerificationEmail()
    {
        var user = await SeedUserAsync();

        await _handler.Handle(
            new UpdateUserCommand(user.Id, null, null, "new@test.com"), CancellationToken.None);

        _emailQueue.Verify(q => q.Enqueue("new@test.com", It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateSameEmail_DoesNotResendVerification()
    {
        var user = await SeedUserAsync("user@test.com");

        await _handler.Handle(
            new UpdateUserCommand(user.Id, null, null, "user@test.com"), CancellationToken.None);

        _emailQueue.Verify(q => q.Enqueue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()), Times.Never);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        var act = () => _handler.Handle(
            new UpdateUserCommand(Guid.NewGuid(), "Name", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_UpdateEmail_DuplicateEmail_ThrowsValidationException()
    {
        await SeedUserAsync("existing@test.com");
        var user = await SeedUserAsync("user@test.com");

        var act = () => _handler.Handle(
            new UpdateUserCommand(user.Id, null, null, "existing@test.com"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
