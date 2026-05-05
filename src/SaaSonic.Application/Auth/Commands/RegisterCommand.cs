using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SaaSonic.Application.Common.Constants;
using SaaSonic.Application.Common.Helpers;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Auth.Commands;

public sealed record RegisterCommand(
    string Email,
    string Password,
    string DisplayName) : IRequest<RegisterResultDto>;

public sealed record RegisterResultDto(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    bool RequiresEmailVerification);

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(100)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit.");

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required.")
            .MaximumLength(200);
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResultDto>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly IEmailQueue _emailQueue;
    private readonly string _emailVerificationUrl;

    public RegisterCommandHandler(
        IAppDbContext db,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        IEmailQueue emailQueue,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _emailQueue = emailQueue;
        _emailVerificationUrl = configuration["App:EmailVerificationUrl"] ?? "http://localhost:5000/auth/verify-email";
    }

    public async Task<RegisterResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == emailLower, cancellationToken))
            throw new Common.Exceptions.ValidationException("An account with this email already exists.");

        var verificationToken = TokenHelper.GenerateSecure();

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = emailLower,
            DisplayName = request.DisplayName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            EmailVerified = false,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            EmailVerificationTokenHash = TokenHelper.Hash(verificationToken),
            EmailVerificationTokenExpiry = DateTimeOffset.UtcNow.AddDays(3),
        };

        var authIdentity = new AuthIdentity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = AuthProvider.Local,
            ProviderUserId = emailLower,
            EmailAtProvider = emailLower,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email);
        var (refreshTokenRaw, refreshTokenHash) = _jwtService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Users.Add(user);
        _db.AuthIdentities.Add(authIdentity);
        _db.RefreshTokens.Add(refreshToken);

        // before SaveChangesAsync — committed atomically with the user.
        // SMTP failure is handled by EmailSenderWorker and does not affect this response.
        _emailQueue.Enqueue(
            toEmail: user.Email,
            templateSlug: EmailTemplateSlug.EmailVerification,
            placeholders: new Dictionary<string, string>
            {
                [EmailTemplatePlaceholder.Name]        = user.DisplayName,
                [EmailTemplatePlaceholder.CallbackUrl] = $"{_emailVerificationUrl}?token={verificationToken}",
                [EmailTemplatePlaceholder.Token]       = verificationToken,
            });

        await _db.SaveChangesAsync(cancellationToken);

        return new RegisterResultDto(user.Id, accessToken, refreshTokenRaw, RequiresEmailVerification: true);
    }
}
