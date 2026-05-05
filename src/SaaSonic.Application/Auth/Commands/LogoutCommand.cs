using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Helpers;
using SaaSonic.Application.Common.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace SaaSonic.Application.Auth.Commands;

public sealed record LogoutCommand(string RefreshToken) : IRequest;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly IAppDbContext _db;

    public LogoutCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHelper.Hash(request.RefreshToken);

        var storedToken = await _db.RefreshTokens
            .AsTracking()
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (storedToken is not null && storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

}
