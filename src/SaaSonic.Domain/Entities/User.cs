namespace SaaSonic.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? PasswordHash { get; set; }
    public bool EmailVerified { get; set; }=false;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string? PasswordResetTokenHash { get; set; }
    public DateTimeOffset? PasswordResetTokenExpiry { get; set; }
    public string? EmailVerificationTokenHash { get; set; }
    public DateTimeOffset? EmailVerificationTokenExpiry { get; set; }

    public Guid? SystemRoleId { get; set; }
    public Role? SystemRole { get; set; }

    public ICollection<AuthIdentity> AuthIdentities { get; set; } = new List<AuthIdentity>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = new List<WorkspaceMember>();
    public ICollection<WorkspaceInvitation> SentWorkspaceInvitations { get; set; } = new List<WorkspaceInvitation>();
    public ICollection<Workspace> OwnedWorkspaces { get; set; } = new List<Workspace>();
}
