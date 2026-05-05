using SaaSonic.Domain.Enums;

namespace SaaSonic.Domain.Entities;

public sealed class WorkspaceMember
{
    public Guid Id { get; set; }
    public Guid WorkspaceId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.Active;
    public Guid? InvitedByUserId { get; set; }
    public DateTimeOffset? JoinedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
    public User? InvitedByUser { get; set; }
}
