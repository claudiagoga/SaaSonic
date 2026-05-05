using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using SaaSonic.Application.Common.Helpers;

namespace SaaSonic.Application.Auth.Commands;

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<RefreshTokenResultDto>;

public sealed record RefreshTokenResultDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn);

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResultDto>
{
    private readonly IAppDbContext _db;
    private readonly IJwtService _jwtService;

    public RefreshTokenCommandHandler(IAppDbContext db, IJwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    public async Task<RefreshTokenResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHelper.Hash(request.RefreshToken);

        var storedToken = await _db.RefreshTokens
            .AsTracking()
            .Include(rt => rt.User).ThenInclude(u => u.SystemRole)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
            throw new UnauthorizedException("Invalid refresh token.");

        if (storedToken.RevokedAt is not null)
        {
            // Token reuse detected — revoke entire family
            await RevokeDescendantsAsync(storedToken, cancellationToken);
            throw new UnauthorizedException("Refresh token has been revoked.");
        }

        if (storedToken.ExpiresAt < DateTimeOffset.UtcNow)
            throw new UnauthorizedException("Refresh token has expired.");

        if (!storedToken.User.IsActive)
            throw new UnauthorizedException("Account is inactive.");

        var newAccessToken = _jwtService.GenerateAccessToken(storedToken.UserId, storedToken.User.Email, storedToken.User.SystemRole?.Name);
        var (newRefreshTokenRaw, newRefreshTokenHash) = _jwtService.GenerateRefreshToken();

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = storedToken.UserId,
            TokenHash = newRefreshTokenHash,
            DeviceId = storedToken.DeviceId,
            DeviceName = storedToken.DeviceName,
            IpAddress = storedToken.IpAddress,
            UserAgent = storedToken.UserAgent,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        storedToken.RevokedAt = DateTimeOffset.UtcNow;
        storedToken.ReplacedByTokenId = newRefreshToken.Id;
        storedToken.LastUsedAt = DateTimeOffset.UtcNow;

        _db.RefreshTokens.Add(newRefreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResultDto(newAccessToken, newRefreshTokenRaw, "Bearer", 3600);
    }

    private async Task RevokeDescendantsAsync(RefreshToken token, CancellationToken ct)
    {
        if (token.ReplacedByTokenId is null) return;

        var child = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Id == token.ReplacedByTokenId, ct);

        if (child is null) return;

        child.RevokedAt = DateTimeOffset.UtcNow;
        await RevokeDescendantsAsync(child, ct);
    }

   }
