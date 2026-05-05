using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Auth.Commands;

public sealed record ExternalLoginCommand(
    AuthProvider Provider,
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    string? AvatarUrl,
    string? ProviderAccessToken,
    string? ProviderRefreshToken,
    DateTimeOffset? ProviderTokenExpiry,
    string? DeviceId = null,
    string? DeviceName = null,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<LoginResultDto>;

public sealed class ExternalLoginCommandHandler : IRequestHandler<ExternalLoginCommand, LoginResultDto>
{
    private readonly IAppDbContext _db;
    private readonly IJwtService _jwtService;

    public ExternalLoginCommandHandler(IAppDbContext db, IJwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    public async Task<LoginResultDto> Handle(ExternalLoginCommand request, CancellationToken cancellationToken)
    {
        var existingIdentity = await _db.AuthIdentities
            .Include(ai => ai.User).ThenInclude(u => u.SystemRole)
            .FirstOrDefaultAsync(ai =>
                ai.Provider == request.Provider &&
                ai.ProviderUserId == request.ProviderUserId,
                cancellationToken);

        User user;

        if (existingIdentity is not null)
        {
            // Update provider tokens
            existingIdentity.ProviderAccessToken = request.ProviderAccessToken;
            existingIdentity.ProviderRefreshToken = request.ProviderRefreshToken;
            existingIdentity.ExpiresAt = request.ProviderTokenExpiry;
            existingIdentity.UpdatedAt = DateTimeOffset.UtcNow;

            user = existingIdentity.User;

            // Update avatar if changed
            if (request.AvatarUrl is not null && user.AvatarUrl != request.AvatarUrl)
            {
                user.AvatarUrl = request.AvatarUrl;
                user.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        else
        {
            // Check if user exists with this email
            User? existingUser = null;
            if (request.Email is not null)
            {
                var emailLower = request.Email.ToLowerInvariant();
                existingUser = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == emailLower, cancellationToken);
            }

            if (existingUser is not null)
            {
                user = existingUser;
            }
            else
            {
                user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = (request.Email ?? $"{request.ProviderUserId}@{request.Provider.ToString().ToLower()}.placeholder").ToLowerInvariant(),
                    DisplayName = request.DisplayName ?? "User",
                    AvatarUrl = request.AvatarUrl,
                    EmailVerified = request.Email is not null,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                };
                _db.Users.Add(user);
            }

            var authIdentity = new AuthIdentity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = request.Provider,
                ProviderUserId = request.ProviderUserId,
                EmailAtProvider = request.Email,
                ProviderAccessToken = request.ProviderAccessToken,
                ProviderRefreshToken = request.ProviderRefreshToken,
                ExpiresAt = request.ProviderTokenExpiry,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            _db.AuthIdentities.Add(authIdentity);
        }

        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.SystemRole?.Name);
        var (refreshTokenRaw, refreshTokenHash) = _jwtService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            DeviceId = request.DeviceId,
            DeviceName = request.DeviceName,
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResultDto(
            AccessToken: accessToken,
            RefreshToken: refreshTokenRaw,
            TokenType: "Bearer",
            ExpiresIn: 3600,
            User: new AuthUserDto(user.Id, user.Email, user.DisplayName, user.AvatarUrl, user.EmailVerified));
    }
}
