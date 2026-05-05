using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;

namespace SaaSonic.Application.Users.Commands;

public sealed record DeleteAccountCommand(
    Guid UserId,
    string Password) : IRequest;

public sealed class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Password).NotEmpty().WithMessage("Password is required to confirm account deletion.");
    }
}

public sealed class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public DeleteAccountCommandHandler(IAppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsTracking()
            .Include(u => u.OwnedWorkspaces)
            .Include(u => u.AuthIdentities)
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);

        if (user is null)
            throw new UnauthorizedException("User not found.");

        if (user.PasswordHash is null)
            throw new ValidationException(
                "This account uses social login. Delete your account via your identity provider.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Incorrect password.");

        if (user.OwnedWorkspaces.Count > 0)
            throw new ValidationException(
                "You must transfer ownership or delete all your workspaces before closing your account.");

        // Anonymise all PII — preserves referential integrity for audit logs and billing records
        user.Email = $"deleted-{user.Id}@deleted.invalid";
        user.DisplayName = "Deleted User";
        user.AvatarUrl = null;
        user.PasswordHash = string.Empty;
        user.EmailVerified = false;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiry = null;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiry = null;
        user.IsActive = false;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        _db.AuthIdentities.RemoveRange(user.AuthIdentities);

        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync(cancellationToken);

        _db.RefreshTokens.RemoveRange(tokens);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
