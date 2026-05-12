using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Tests.Common;
using SaaSonic.Application.Users.Queries;
using SaaSonic.Domain.Entities;

namespace SaaSonic.Application.Tests.Users.Queries;

public class GetMyProfileQueryHandlerTests : IDisposable
{
    private readonly TestDbContext _db;
    private readonly GetMyProfileQueryHandler _handler;

    public GetMyProfileQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new TestDbContext(options);
        _handler = new GetMyProfileQueryHandler(_db);
    }

    public void Dispose() => _db.Dispose();

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<User> SeedUserAsync(bool isActive = true)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "user@test.com",
            DisplayName = "Test User",
            AvatarUrl = "https://example.com/avatar.png",
            EmailVerified = true,
            IsActive = isActive,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return user;
    }

    // ── happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ActiveUser_ReturnsProfile()
    {
        var user = await SeedUserAsync();

        var result = await _handler.Handle(new GetMyProfileQuery(user.Id), CancellationToken.None);

        result.Id.Should().Be(user.Id);
        result.Email.Should().Be(user.Email);
        result.DisplayName.Should().Be(user.DisplayName);
        result.AvatarUrl.Should().Be(user.AvatarUrl);
        result.EmailVerified.Should().BeTrue();
    }

    // ── failure paths ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_UserNotFound_ThrowsUnauthorizedException()
    {
        var act = () => _handler.Handle(new GetMyProfileQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task Handle_InactiveUser_ThrowsUnauthorizedException()
    {
        var user = await SeedUserAsync(isActive: false);

        var act = () => _handler.Handle(new GetMyProfileQuery(user.Id), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
