using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class AuthIdentity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AuthProvider Provider { get; set; }
    public string ProviderUserId { get; set; } = string.Empty;
    public string? EmailAtProvider { get; set; }
    public string? ProviderAccessToken { get; set; }
    public string? ProviderRefreshToken { get; set; }
   public DateTimeOffset? ExpiresAt { get; set; } 
    public DateTimeOffset CreatedAt { get; set; }
     public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
