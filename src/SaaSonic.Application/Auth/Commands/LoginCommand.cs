using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SaaSonic.Application.Common.Exceptions;
using SaaSonic.Application.Common.Interfaces;
using SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

namespace SaaSonic.Application.Auth.Commands;

public sealed record LoginCommand(
    string Email,
    string Password,
    string? DeviceId = null,
    string? DeviceName = null,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<LoginResultDto>;

public sealed record LoginResultDto(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    AuthUserDto User);

public sealed record AuthUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string? AvatarUrl,
    bool EmailVerified);

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResultDto>
{
    private readonly IAppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public LoginCommandHandler(IAppDbContext db, IPasswordHasher passwordHasher, IJwtService jwtService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<LoginResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.SystemRole)
            .FirstOrDefaultAsync(u => u.Email == emailLower, cancellationToken);

        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid email or password.");

        if (user.PasswordHash is null)
            throw new UnauthorizedException("This account uses social login. Please sign in with your provider.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

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
