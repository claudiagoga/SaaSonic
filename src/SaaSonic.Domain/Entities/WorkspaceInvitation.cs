namespace SaaSonic.Domain.Entities;
using SaaSonic.Domain.Enums;

public sealed class WorkspaceInvitation
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
   public Guid InvitedByUserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public WorkspaceInvitationStatus Status { get; set; } = WorkspaceInvitationStatus.Pending;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User InvitedByUser { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
