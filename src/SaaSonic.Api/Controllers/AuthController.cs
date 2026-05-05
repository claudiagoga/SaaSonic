using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using SaaSonic.Application.Auth.Commands;
using SaaSonic.Domain.Enums;
using System.Security.Claims;

namespace SaaSonic.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

   
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RegisterCommand(request.Email, request.Password, request.DisplayName), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }


    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new LoginCommand(
            request.Email,
            request.Password,
            DeviceId: request.DeviceId,
            DeviceName: request.DeviceName,
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: Request.Headers.UserAgent.ToString()), ct);
        return Ok(result);
    }

    /// Exchange a refresh token for a new access + refresh token pair.
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(RefreshTokenResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken), ct);
        return Ok(result);
    }


    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken), ct);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email), ct);
        return NoContent();
    }

    
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword), ct);
        return NoContent();
    }

   
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        await _mediator.Send(new VerifyEmailCommand(request.Token), ct);
        return NoContent();
    }

    /// <summary>Initiate OAuth login with an external provider. Redirects to the provider.</summary>
    [HttpGet("external/{provider}")]
    public IActionResult ExternalLogin(string provider, [FromQuery] string? returnUrl = null)
    {
        if (!IsValidProvider(provider))
            return BadRequest($"Unsupported provider: {provider}");

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), new { provider, returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, provider);
    }

    /// <summary>OAuth callback — called by the provider after user authenticates.</summary>
    [HttpGet("external/{provider}/callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> ExternalLoginCallback(
        string provider,
        [FromQuery] string? returnUrl = null,
        CancellationToken ct = default)
    {
        var authResult = await HttpContext.AuthenticateAsync("ExternalCookie");
        if (!authResult.Succeeded)
            return Unauthorized("External authentication failed.");

        await HttpContext.SignOutAsync("ExternalCookie");

        var principal = authResult.Principal;
        var providerUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (providerUserId is null)
            return Unauthorized("Could not retrieve provider user ID.");

        var authProvider = Enum.Parse<AuthProvider>(provider, ignoreCase: true);
        var providerToken = authResult.Properties?.GetTokenValue("access_token");
        var providerRefreshToken = authResult.Properties?.GetTokenValue("refresh_token");
        var expiresAt = authResult.Properties?.GetTokenValue("expires_at") is string exp
            ? DateTimeOffset.TryParse(exp, out var parsed) ? parsed : (DateTimeOffset?)null
            : null;

        var result = await _mediator.Send(new ExternalLoginCommand(
            Provider: authProvider,
            ProviderUserId: providerUserId,
            Email: principal.FindFirstValue(ClaimTypes.Email),
            DisplayName: principal.FindFirstValue(ClaimTypes.Name),
            AvatarUrl: principal.FindFirstValue("urn:google:picture") ?? principal.FindFirstValue("picture"),
            ProviderAccessToken: providerToken,
            ProviderRefreshToken: providerRefreshToken,
            ProviderTokenExpiry: expiresAt,
            IpAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent: Request.Headers.UserAgent.ToString()), ct);

        var redirect = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        return Redirect($"{redirect}?access_token={result.AccessToken}&refresh_token={result.RefreshToken}");
    }

    private static bool IsValidProvider(string provider) =>
        provider.Equals("google", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("facebook", StringComparison.OrdinalIgnoreCase) ||
        provider.Equals("github", StringComparison.OrdinalIgnoreCase);
}

// --- Request / Response DTOs ---

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password, string? DeviceId = null, string? DeviceName = null);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record LogoutRequest(string RefreshToken);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Token, string NewPassword);
public sealed record VerifyEmailRequest(string Token);