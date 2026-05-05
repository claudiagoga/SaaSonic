using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SaaSonic.Application.Common.Constants;
using SaaSonic.Application.Common.Helpers;
using SaaSonic.Application.Common.Interfaces;

namespace SaaSonic.Application.Auth.Commands;

public sealed record ForgotPasswordCommand(string Email) : IRequest;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly IAppDbContext _db;
    private readonly IEmailQueue _emailQueue;
    private readonly string _passwordResetUrl;

    public ForgotPasswordCommandHandler(
        IAppDbContext db,
        IEmailQueue emailQueue,
        IConfiguration configuration)
    {
        _db = db;
        _emailQueue = emailQueue;
        _passwordResetUrl = configuration["App:PasswordResetUrl"] ?? "http://localhost:5000/auth/reset-password";
    }

    public async Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.ToLowerInvariant();
        var user = await _db.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Email == emailLower && u.IsActive, cancellationToken);

        // Always return success — prevents email enumeration attacks
        if (user is null) return;

        var resetToken = TokenHelper.GenerateSecure();

        user.PasswordResetTokenHash = TokenHelper.Hash(resetToken);
        user.PasswordResetTokenExpiry = DateTimeOffset.UtcNow.AddHours(1);
        user.UpdatedAt = DateTimeOffset.UtcNow;

        // Staged before SaveChangesAsync — token and email are committed atomically.
        _emailQueue.Enqueue(
            toEmail: user.Email,
            templateSlug: EmailTemplateSlug.PasswordReset,
            placeholders: new Dictionary<string, string>
            {
                [EmailTemplatePlaceholder.Name]        = user.DisplayName,
                [EmailTemplatePlaceholder.CallbackUrl] = $"{_passwordResetUrl}?token={resetToken}",
                [EmailTemplatePlaceholder.Token]       = resetToken,
            });

        await _db.SaveChangesAsync(cancellationToken);
    }
}
