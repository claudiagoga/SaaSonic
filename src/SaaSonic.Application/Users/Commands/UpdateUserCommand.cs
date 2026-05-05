using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Helpers;
using ValidationException = SaaSonic.Application.Common.Exceptions.ValidationException;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Application.Users.Queries;
using SaaSonic.Application.Common.Constants;

namespace SaaSonic.Application.Users.Commands;

public sealed record UpdateUserCommand(
    Guid UserId,
    string? DisplayName,
    string? AvatarUrl,
    string? Email) : IRequest<UserProfileDto>;

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        // At least one field must be provided
        RuleFor(x => x)
            .Must(x => x.DisplayName is not null || x.AvatarUrl is not null || x.Email is not null)
            .WithMessage("At least one field must be provided.");

        When(x => x.DisplayName is not null, () =>
        {
            RuleFor(x => x.DisplayName)
                .NotEmpty().WithMessage("Display name cannot be empty.")
                .MaximumLength(200);
        });

        When(x => x.AvatarUrl is not null, () =>
        {
            RuleFor(x => x.AvatarUrl)
                .MaximumLength(2048)
                .Must(url => string.IsNullOrEmpty(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("Avatar URL must be a valid URL.");
        });

        When(x => x.Email is not null, () =>
        {
            RuleFor(x => x.Email)
                .NotEmpty().WithMessage("Email cannot be empty.")
                .EmailAddress().WithMessage("A valid email address is required.")
                .MaximumLength(256);
        });
    }
}

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserProfileDto>
{
    private readonly IAppDbContext _db;
    private readonly IEmailQueue _emailQueue;
    private readonly string _emailVerificationUrl;

    public UpdateUserCommandHandler(
        IAppDbContext db,
        IEmailQueue emailQueue,
        IConfiguration configuration)
    {
        _db = db;
        _emailQueue = emailQueue;
        _emailVerificationUrl = configuration["App:EmailVerificationUrl"] ?? "http://localhost:5000/auth/verify-email";
    }

    public async Task<UserProfileDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .AsTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive, cancellationToken);

        if (user is null)
            throw new UnauthorizedException("User not found.");

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName;

        // null = no change; empty string = clear the avatar
        if (request.AvatarUrl is not null)
            user.AvatarUrl = string.IsNullOrEmpty(request.AvatarUrl) ? null : request.AvatarUrl;

        if (request.Email is not null)
        {
            var newEmail = request.Email.ToLowerInvariant();

            if (newEmail != user.Email)
            {
                if (await _db.Users.AnyAsync(u => u.Email == newEmail && u.Id != user.Id, cancellationToken))
                    throw new ValidationException("An account with this email already exists.");

                var verificationToken = TokenHelper.GenerateSecure();

                user.Email = newEmail;
                user.EmailVerified = false;
                user.EmailVerificationTokenHash = TokenHelper.Hash(verificationToken);
                user.EmailVerificationTokenExpiry = DateTimeOffset.UtcNow.AddDays(3);

                // Staged before SaveChangesAsync — committed atomically with the email change.
                _emailQueue.Enqueue(
                    toEmail: newEmail,
                    templateSlug: EmailTemplateSlug.EmailVerification,
                    placeholders: new Dictionary<string, string>
                    {
                        [EmailTemplatePlaceholder.Name]        = user.DisplayName,
                        [EmailTemplatePlaceholder.CallbackUrl] = $"{_emailVerificationUrl}?token={verificationToken}",
                        [EmailTemplatePlaceholder.Token]       = verificationToken,
                    });
            }
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new UserProfileDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.AvatarUrl,
            user.EmailVerified,
            user.CreatedAt);
    }
}
