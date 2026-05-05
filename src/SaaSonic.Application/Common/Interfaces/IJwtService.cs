using System.Security.Claims;

namespace SaaSonic.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email, string? systemRole = null);
    (string Token, string Hash) GenerateRefreshToken();
    ClaimsPrincipal? ValidateExpiredToken(string token);
}
