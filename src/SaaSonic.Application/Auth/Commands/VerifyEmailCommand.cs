using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Common.Helpers;
using System.Security.Cryptography;
using System.Security.Principal;

namespace SaaSonic.Application.Auth.Commands;

public sealed record VerifyEmailCommand(string Token) : IRequest;

public sealed class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand>
{
    private readonly IAppDbContext _db;

    public VerifyEmailCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = TokenHelper.Hash(request.Token);

        var user = await _db.Users
            .AsTracking()
            .FirstOrDefaultAsync(u =>
                u.EmailVerificationTokenHash == tokenHash &&
                u.EmailVerificationTokenExpiry > DateTimeOffset.UtcNow,
                cancellationToken);

        if (user is null)
            throw new UnauthorizedException("Invalid or expired email verification token.");

        if (user.EmailVerified)
        {
            // Already verified — clear the token and return success
            user.EmailVerificationTokenHash = null;
            user.EmailVerificationTokenExpiry = null;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        user.EmailVerified = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiry = null;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

}
