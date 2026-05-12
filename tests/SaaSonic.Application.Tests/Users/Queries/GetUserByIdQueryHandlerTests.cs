using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Users.Queries;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Users.Queries;

public class GetUserByIdQueryHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new GetUserByIdQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
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
    public async Task Handle_ExistingUser_ReturnsProfile()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(new GetUserByIdQuery(user.Id), CancellationToken.None);

        result.Id.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.DisplayName.Should().Be(user.DisplayName);
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ThrowsValidationException()
    {
        var act = () => _handler.Handle(new GetUserByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
