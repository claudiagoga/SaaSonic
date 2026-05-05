using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Application.Common.Constants;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Auth.Commands;

public class ForgotPasswordCommandHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly Mock<IEmailQueue> _emailQueue = new();
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly ForgotPasswordCommandHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _configuration.Setup(c => c["App:FrontendUrl"]).Returns("http://localhost:3000");
        _handler = new ForgotPasswordCommandHandler(_db, _emailQueue.Object, _configuration.Object);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(string email = "user@test.com", bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Test User",
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExistingActiveUser_SetsResetTokenOnUser()
    {
        await SeedUserAsync();

        await _handler.Handle(new ForgotPasswordCommand("user@test.com"), CancellationToken.None);

        var user = await _db.Users.SingleAsync();
        user.PasswordResetTokenHash.Should().NotBeNullOrEmpty();
        user.PasswordResetTokenExpiry.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_ExistingActiveUser_SendsPasswordResetEmail()
    {
        await SeedUserAsync();

        await _handler.Handle(new ForgotPasswordCommand("user@test.com"), CancellationToken.None);

        _emailQueue.Verify(q => q.Enqueue(
            "user@test.com",
            EmailTemplateSlug.PasswordReset,
            It.IsAny<Dictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingActiveUser_CallbackUrlContainsFrontendUrl()
    {
        await SeedUserAsync();
        string? capturedCallbackUrl = null;

        _emailQueue
            .Setup(q => q.Enqueue(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()))
            .Callback<string, string, Dictionary<string, string>>(
                (_, _, placeholders) =>
                    capturedCallbackUrl = placeholders.GetValueOrDefault(EmailTemplatePlaceholder.CallbackUrl));

        await _handler.Handle(new ForgotPasswordCommand("user@test.com"), CancellationToken.None);

        capturedCallbackUrl.Should().StartWith("http://localhost:3000");
    }

    // ── email enumeration protection ─────────────────────────────────────────

    [Fact]
    public async Task Handle_EmailNotFound_CompletesWithoutError()
    {
        // The handler deliberately returns success for unknown emails to prevent
        // attackers from discovering which emails are registered.
        var act = () => _handler.Handle(
            new ForgotPasswordCommand("nobody@test.com"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_EmailNotFound_DoesNotSendEmail()
    {
        await _handler.Handle(new ForgotPasswordCommand("nobody@test.com"), CancellationToken.None);

        _emailQueue.Verify(
            q => q.Enqueue(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_InactiveUser_CompletesWithoutError()
    {
        // Inactive accounts are treated the same as non-existent ones.
        await SeedUserAsync(isActive: false);

        var act = () => _handler.Handle(
            new ForgotPasswordCommand("user@test.com"), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _emailQueue.Verify(
            q => q.Enqueue(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, string>>()),
            Times.Never);
    }
}
